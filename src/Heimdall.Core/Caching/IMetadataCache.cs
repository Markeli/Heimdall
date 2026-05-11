namespace Heimdall.Core.Caching;

/// <summary>
/// In-memory metadata cache abstraction. Stores upstream metadata payloads keyed by string
/// with a time-to-live, so they can be served without re-hitting the upstream feed.
/// </summary>
public interface IMetadataCache
{
	/// <summary>
	/// Retrieves a previously stored value by key.
	/// </summary>
	/// <typeparam name="T">Reference type of the cached value.</typeparam>
	/// <param name="key">Cache key.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The cached value, or <c>null</c> if absent or expired.</returns>
	ValueTask<T?> GetAsync<T>(string key, CancellationToken ct) where T : class;

	/// <summary>
	/// Stores a value under the given key with the specified time-to-live.
	/// </summary>
	/// <typeparam name="T">Reference type of the value to cache.</typeparam>
	/// <param name="key">Cache key.</param>
	/// <param name="value">Value to store.</param>
	/// <param name="ttl">Time-to-live after which the entry is considered expired.</param>
	/// <param name="ct">Cancellation token.</param>
	ValueTask SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class;

	/// <summary>
	/// Removes the entry associated with the given key, if any.
	/// </summary>
	/// <param name="key">Cache key.</param>
	/// <param name="ct">Cancellation token.</param>
	ValueTask RemoveAsync(string key, CancellationToken ct);
}
