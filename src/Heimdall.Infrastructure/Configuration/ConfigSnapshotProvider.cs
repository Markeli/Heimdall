using Heimdall.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Heimdall.Infrastructure.Configuration;

/// <summary>
/// Produces immutable <see cref="ConfigSnapshot"/> values from the current
/// <see cref="HeimdallOptions"/> together with the active <see cref="IConfigGeneration"/>.
/// Each snapshot is a consistent view tied to a specific generation.
/// </summary>
public sealed class ConfigSnapshotProvider : IConfigSnapshotProvider
{
	private readonly IOptionsMonitor<HeimdallOptions> _monitor;
	private readonly IConfigGeneration _generation;

	/// <summary>Creates a snapshot provider over the supplied options monitor and generation counter.</summary>
	/// <param name="monitor">Options monitor for <see cref="HeimdallOptions"/>.</param>
	/// <param name="generation">Generation counter bumped on each options reload.</param>
	/// <exception cref="ArgumentNullException">Either dependency is <c>null</c>.</exception>
	public ConfigSnapshotProvider(IOptionsMonitor<HeimdallOptions> monitor, IConfigGeneration generation)
	{
		ArgumentNullException.ThrowIfNull(monitor);
		ArgumentNullException.ThrowIfNull(generation);
		_monitor = monitor;
		_generation = generation;
	}

	/// <inheritdoc />
	public ConfigSnapshot Capture()
	{
		var options = _monitor.CurrentValue;
		var feeds = FeedConfigMapper.Map(options);
		return new ConfigSnapshot(_generation.Current, feeds);
	}
}
