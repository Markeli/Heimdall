using Heimdall.Core.Caching;

namespace Heimdall.Infrastructure.Caching;

/// <summary>
/// Two-layer <see cref="IMetadataCache"/> implementation that reads L1 first and
/// falls back to L2, promoting L2 hits into L1 so subsequent reads stay in-process.
/// Writes and removes fan out to both strands.
/// </summary>
public sealed class HybridMetadataCache : IMetadataCache
{
	private readonly ICacheLayer _l1;
	private readonly ICacheLayer _l2;

	/// <summary>Creates a hybrid cache composed of the given L1 and L2 strands.</summary>
	/// <param name="l1">Fast in-process strand (typically <see cref="MemoryCacheL1"/>).</param>
	/// <param name="l2">Slower shared strand (e.g. distributed cache, or a no-op placeholder).</param>
	/// <exception cref="ArgumentNullException">Either strand is <c>null</c>.</exception>
	public HybridMetadataCache(ICacheLayer l1, ICacheLayer l2)
	{
		ArgumentNullException.ThrowIfNull(l1);
		ArgumentNullException.ThrowIfNull(l2);
		_l1 = l1;
		_l2 = l2;
	}

	/// <inheritdoc />
	public async ValueTask<T?> GetAsync<T>(string key, CancellationToken ct) where T : class
	{
		var l1Hit = await _l1.GetAsync<T>(key, ct);
		if (l1Hit is not null)
		{
			return l1Hit;
		}

		var l2Hit = await _l2.GetAsync<T>(key, ct);
		if (l2Hit is not null)
		{
			// Promote with a short TTL: the authoritative TTL lives on L2, L1 is just a hot-path mirror.
			await _l1.SetAsync(key, l2Hit, DefaultPromotionTtl, ct);
		}

		return l2Hit;
	}

	/// <inheritdoc />
	public async ValueTask SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class
	{
		await _l1.SetAsync(key, value, ttl, ct);
		await _l2.SetAsync(key, value, ttl, ct);
	}

	/// <inheritdoc />
	public async ValueTask RemoveAsync(string key, CancellationToken ct)
	{
		await _l1.RemoveAsync(key, ct);
		await _l2.RemoveAsync(key, ct);
	}

	private static readonly TimeSpan DefaultPromotionTtl = TimeSpan.FromMinutes(1);
}
