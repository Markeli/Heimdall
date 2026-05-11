using Microsoft.Extensions.Caching.Memory;

namespace Heimdall.Infrastructure.Caching;

public sealed class MemoryCacheL1 : ICacheLayer
{
	private readonly IMemoryCache _cache;

	public MemoryCacheL1(IMemoryCache cache)
	{
		ArgumentNullException.ThrowIfNull(cache);
		_cache = cache;
	}

	public ValueTask<T?> GetAsync<T>(string key, CancellationToken ct) where T : class
	{
		ct.ThrowIfCancellationRequested();
		_cache.TryGetValue(key, out var value);
		return ValueTask.FromResult(value as T);
	}

	public ValueTask SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class
	{
		ct.ThrowIfCancellationRequested();
		_cache.Set(key, value, ttl);
		return ValueTask.CompletedTask;
	}

	public ValueTask RemoveAsync(string key, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		_cache.Remove(key);
		return ValueTask.CompletedTask;
	}
}
