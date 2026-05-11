using Heimdall.Application.Caching;

namespace Heimdall.Infrastructure.Caching;

public sealed class HybridMetadataCache : IMetadataCache
{
	private readonly ICacheLayer _l1;
	private readonly ICacheLayer _l2;

	public HybridMetadataCache(ICacheLayer l1, ICacheLayer l2)
	{
		ArgumentNullException.ThrowIfNull(l1);
		ArgumentNullException.ThrowIfNull(l2);
		_l1 = l1;
		_l2 = l2;
	}

	public async ValueTask<T?> GetAsync<T>(string key, CancellationToken ct) where T : class
	{
		var l1Hit = await _l1.GetAsync<T>(key, ct).ConfigureAwait(false);
		if (l1Hit is not null)
		{
			return l1Hit;
		}

		var l2Hit = await _l2.GetAsync<T>(key, ct).ConfigureAwait(false);
		if (l2Hit is not null)
		{
			await _l1.SetAsync(key, l2Hit, DefaultPromotionTtl, ct).ConfigureAwait(false);
		}

		return l2Hit;
	}

	public async ValueTask SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class
	{
		await _l1.SetAsync(key, value, ttl, ct).ConfigureAwait(false);
		await _l2.SetAsync(key, value, ttl, ct).ConfigureAwait(false);
	}

	public async ValueTask RemoveAsync(string key, CancellationToken ct)
	{
		await _l1.RemoveAsync(key, ct).ConfigureAwait(false);
		await _l2.RemoveAsync(key, ct).ConfigureAwait(false);
	}

	private static readonly TimeSpan DefaultPromotionTtl = TimeSpan.FromMinutes(1);
}
