namespace Heimdall.Core.Caching;

public interface IMetadataCache
{
	ValueTask<T?> GetAsync<T>(string key, CancellationToken ct) where T : class;
	ValueTask SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class;
	ValueTask RemoveAsync(string key, CancellationToken ct);
}
