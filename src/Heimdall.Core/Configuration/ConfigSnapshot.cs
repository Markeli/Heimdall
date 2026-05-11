using Heimdall.Core.Configuration;

namespace Heimdall.Core.Configuration;

/// <summary>
/// Immutable point-in-time view of the active configuration. Includes a monotonic generation
/// number so callers can detect when configuration has been reloaded.
/// </summary>
/// <param name="Generation">Monotonic version stamp of this snapshot.</param>
/// <param name="Feeds">All configured feeds visible at the time of capture.</param>
public sealed record ConfigSnapshot(long Generation, IReadOnlyList<FeedConfig> Feeds);
