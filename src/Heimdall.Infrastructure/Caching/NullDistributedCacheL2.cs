namespace Heimdall.Infrastructure.Caching;

/// <summary>
/// No-op L2 cache strand used as a contract placeholder until a real distributed
/// backend (e.g. Redis) is wired up. Always misses on get and silently ignores writes.
/// </summary>
public sealed class NullDistributedCacheL2 : ICacheLayer
{
	/// <inheritdoc />
	public ValueTask<T?> GetAsync<T>(string key, CancellationToken ct) where T : class =>
		ValueTask.FromResult<T?>(null);

	/// <inheritdoc />
	public ValueTask SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class =>
		ValueTask.CompletedTask;

	/// <inheritdoc />
	public ValueTask RemoveAsync(string key, CancellationToken ct) =>
		ValueTask.CompletedTask;
}
