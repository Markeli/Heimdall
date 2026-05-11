using Microsoft.Extensions.Caching.Memory;

namespace Heimdall.Infrastructure.Caching;

/// <summary>
/// In-process L1 cache strand backed by <see cref="IMemoryCache"/>.
/// </summary>
public sealed class MemoryCacheL1 : ICacheLayer
{
	private readonly IMemoryCache _cache;

	/// <summary>Creates a new L1 cache strand over the provided <see cref="IMemoryCache"/>.</summary>
	/// <param name="cache">Underlying in-memory cache instance.</param>
	/// <exception cref="ArgumentNullException"><paramref name="cache"/> is <c>null</c>.</exception>
	public MemoryCacheL1(IMemoryCache cache)
	{
		ArgumentNullException.ThrowIfNull(cache);
		_cache = cache;
	}

	/// <inheritdoc />
	/// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
	public ValueTask<T?> GetAsync<T>(string key, CancellationToken ct) where T : class
	{
		ct.ThrowIfCancellationRequested();
		_cache.TryGetValue(key, out var value);
		return ValueTask.FromResult(value as T);
	}

	/// <inheritdoc />
	/// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
	public ValueTask SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class
	{
		ct.ThrowIfCancellationRequested();
		_cache.Set(key, value, ttl);
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc />
	/// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
	public ValueTask RemoveAsync(string key, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		_cache.Remove(key);
		return ValueTask.CompletedTask;
	}
}
