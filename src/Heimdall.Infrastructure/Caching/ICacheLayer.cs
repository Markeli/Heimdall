namespace Heimdall.Infrastructure.Caching;

/// <summary>
/// Single strand of a layered cache (e.g. L1 in-memory or L2 distributed).
/// Implementations are composed by <see cref="HybridMetadataCache"/>.
/// </summary>
public interface ICacheLayer
{
	/// <summary>Returns the cached value for the key, or <c>null</c> if absent.</summary>
	/// <typeparam name="T">Reference type of the cached value.</typeparam>
	/// <param name="key">Cache key.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The cached value, or <c>null</c> on miss.</returns>
	ValueTask<T?> GetAsync<T>(string key, CancellationToken ct) where T : class;

	/// <summary>Stores a value under the given key with the supplied TTL.</summary>
	/// <typeparam name="T">Reference type of the cached value.</typeparam>
	/// <param name="key">Cache key.</param>
	/// <param name="value">Value to store.</param>
	/// <param name="ttl">Time-to-live for this entry.</param>
	/// <param name="ct">Cancellation token.</param>
	ValueTask SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class;

	/// <summary>Removes any entry stored under the given key.</summary>
	/// <param name="key">Cache key.</param>
	/// <param name="ct">Cancellation token.</param>
	ValueTask RemoveAsync(string key, CancellationToken ct);
}
