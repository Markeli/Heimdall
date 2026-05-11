namespace Heimdall.Infrastructure.Caching;

public sealed class NullDistributedCacheL2 : ICacheLayer
{
	public ValueTask<T?> GetAsync<T>(string key, CancellationToken ct) where T : class =>
		ValueTask.FromResult<T?>(null);

	public ValueTask SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class =>
		ValueTask.CompletedTask;

	public ValueTask RemoveAsync(string key, CancellationToken ct) =>
		ValueTask.CompletedTask;
}
