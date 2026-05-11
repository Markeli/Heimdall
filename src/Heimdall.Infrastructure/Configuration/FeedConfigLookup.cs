using System.Diagnostics.CodeAnalysis;
using Heimdall.Core.Configuration;

namespace Heimdall.Infrastructure.Configuration;

/// <summary>
/// Resolves a <see cref="FeedConfig"/> by (ecosystem, feed name) against the
/// current <see cref="ConfigSnapshot"/>. Matching is case-insensitive.
/// </summary>
public sealed class FeedConfigLookup : IFeedConfigLookup
{
	private readonly IConfigSnapshotProvider _snapshots;

	/// <summary>Creates a lookup backed by the provided snapshot source.</summary>
	/// <param name="snapshots">Provider used to capture the active configuration snapshot.</param>
	/// <exception cref="ArgumentNullException"><paramref name="snapshots"/> is <c>null</c>.</exception>
	public FeedConfigLookup(IConfigSnapshotProvider snapshots)
	{
		ArgumentNullException.ThrowIfNull(snapshots);
		_snapshots = snapshots;
	}

	/// <inheritdoc />
	public bool TryGet(string ecosystem, string feedName, [NotNullWhen(true)] out FeedConfig? config)
	{
		var snapshot = _snapshots.Capture();
		foreach (var feed in snapshot.Feeds)
		{
			if (string.Equals(feed.Ecosystem, ecosystem, StringComparison.OrdinalIgnoreCase)
				&& string.Equals(feed.Name, feedName, StringComparison.OrdinalIgnoreCase))
			{
				config = feed;
				return true;
			}
		}

		config = null;
		return false;
	}
}
