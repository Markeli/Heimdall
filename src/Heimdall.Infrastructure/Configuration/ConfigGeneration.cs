using Heimdall.Application.Configuration;
using Microsoft.Extensions.Options;

namespace Heimdall.Infrastructure.Configuration;

public sealed class ConfigGeneration : IConfigGeneration, IDisposable
{
	private long _current;
	private readonly IDisposable? _subscription;

	public ConfigGeneration(IOptionsMonitor<HeimdallOptions> monitor)
	{
		ArgumentNullException.ThrowIfNull(monitor);
		_subscription = monitor.OnChange(_ => Interlocked.Increment(ref _current));
	}

	public long Current => Interlocked.Read(ref _current);

	public void Dispose() => _subscription?.Dispose();
}
