using Heimdall.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Heimdall.Infrastructure.Configuration;

/// <summary>
/// Monotonic counter that increments on every <see cref="HeimdallOptions"/> reload.
/// Downstream components embed <see cref="Current"/> in cache keys to invalidate
/// stale entries cheaply after a YAML config change.
/// </summary>
public sealed class ConfigGeneration : IConfigGeneration, IDisposable
{
	private long _current;
	private readonly IDisposable? _subscription;

	/// <summary>Subscribes to options reloads so the generation bumps on each change.</summary>
	/// <param name="monitor">Options monitor for <see cref="HeimdallOptions"/>.</param>
	/// <exception cref="ArgumentNullException"><paramref name="monitor"/> is <c>null</c>.</exception>
	public ConfigGeneration(IOptionsMonitor<HeimdallOptions> monitor)
	{
		ArgumentNullException.ThrowIfNull(monitor);
		// Bump on reload so cache keys/snapshots built against the old options expire naturally.
		_subscription = monitor.OnChange(_ => Interlocked.Increment(ref _current));
	}

	/// <summary>Current generation value; monotonically increasing across the process lifetime.</summary>
	public long Current => Interlocked.Read(ref _current);

	/// <summary>Releases the options reload subscription.</summary>
	public void Dispose() => _subscription?.Dispose();
}
