using Heimdall.Application.Configuration;
using Microsoft.Extensions.Options;

namespace Heimdall.Infrastructure.Configuration;

public sealed class ConfigSnapshotProvider : IConfigSnapshotProvider
{
	private readonly IOptionsMonitor<HeimdallOptions> _monitor;
	private readonly IConfigGeneration _generation;

	public ConfigSnapshotProvider(IOptionsMonitor<HeimdallOptions> monitor, IConfigGeneration generation)
	{
		ArgumentNullException.ThrowIfNull(monitor);
		ArgumentNullException.ThrowIfNull(generation);
		_monitor = monitor;
		_generation = generation;
	}

	public ConfigSnapshot Capture()
	{
		var options = _monitor.CurrentValue;
		var feeds = FeedConfigMapper.Map(options);
		return new ConfigSnapshot(_generation.Current, feeds);
	}
}
