namespace Heimdall.Core.Configuration;

/// <summary>
/// Source of immutable configuration snapshots. Each call returns a coherent view of the
/// configuration as it exists at that moment.
/// </summary>
public interface IConfigSnapshotProvider
{
	/// <summary>
	/// Captures the current configuration state.
	/// </summary>
	/// <returns>A snapshot reflecting the active configuration at the time of the call.</returns>
	ConfigSnapshot Capture();
}
