using Heimdall.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;

namespace Heimdall.UnitTests.Caching;

public class HybridMetadataCacheTests
{
	private sealed record Box(string Value);

	private static MemoryCacheL1 NewL1() =>
		new(new MemoryCache(new MemoryCacheOptions()));

	[Fact]
	public async Task Get_returns_l1_hit_without_consulting_l2()
	{
		var l1 = NewL1();
		var l2 = Substitute.For<ICacheLayer>();
		var cache = new HybridMetadataCache(l1, l2);

		await cache.SetAsync("k", new Box("v"), TimeSpan.FromMinutes(1), default);
		l2.ClearReceivedCalls();

		var result = await cache.GetAsync<Box>("k", default);

		result.Should().NotBeNull();
		result!.Value.Should().Be("v");
		await l2.DidNotReceive().GetAsync<Box>(Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_promotes_l2_hit_to_l1()
	{
		var l1 = NewL1();
		var l2 = Substitute.For<ICacheLayer>();
		l2.GetAsync<Box>("k", Arg.Any<CancellationToken>()).Returns(new ValueTask<Box?>(new Box("v2")));
		var cache = new HybridMetadataCache(l1, l2);

		var first = await cache.GetAsync<Box>("k", default);
		first!.Value.Should().Be("v2");

		l2.ClearReceivedCalls();
		var second = await cache.GetAsync<Box>("k", default);

		second!.Value.Should().Be("v2");
		await l2.DidNotReceive().GetAsync<Box>(Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Get_returns_null_on_miss_in_both()
	{
		var cache = new HybridMetadataCache(NewL1(), new NullDistributedCacheL2());

		var result = await cache.GetAsync<Box>("missing", default);

		result.Should().BeNull();
	}

	[Fact]
	public async Task Set_writes_to_both_layers()
	{
		var l1 = Substitute.For<ICacheLayer>();
		var l2 = Substitute.For<ICacheLayer>();
		var cache = new HybridMetadataCache(l1, l2);

		await cache.SetAsync("k", new Box("v"), TimeSpan.FromMinutes(5), default);

		await l1.Received(1).SetAsync(
			"k", Arg.Any<Box>(), TimeSpan.FromMinutes(5), Arg.Any<CancellationToken>());
		await l2.Received(1).SetAsync(
			"k", Arg.Any<Box>(), TimeSpan.FromMinutes(5), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Remove_clears_both_layers()
	{
		var l1 = Substitute.For<ICacheLayer>();
		var l2 = Substitute.For<ICacheLayer>();
		var cache = new HybridMetadataCache(l1, l2);

		await cache.RemoveAsync("k", default);

		await l1.Received(1).RemoveAsync("k", Arg.Any<CancellationToken>());
		await l2.Received(1).RemoveAsync("k", Arg.Any<CancellationToken>());
	}
}
