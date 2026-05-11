namespace Heimdall.Core.Configuration;

/// <summary>
/// Exposes the current configuration generation number. The value increases monotonically
/// each time configuration is reloaded.
/// </summary>
public interface IConfigGeneration
{
	/// <summary>
	/// Current generation stamp of the active configuration.
	/// </summary>
	long Current { get; }
}
