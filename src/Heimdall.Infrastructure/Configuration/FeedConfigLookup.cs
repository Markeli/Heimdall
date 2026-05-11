using System.Diagnostics.CodeAnalysis;
using Heimdall.Application.Configuration;
using Heimdall.Domain.Configuration;

namespace Heimdall.Infrastructure.Configuration;

public sealed class FeedConfigLookup : IFeedConfigLookup
{
	private readonly IConfigSnapshotProvider _snapshots;

	public FeedConfigLookup(IConfigSnapshotProvider snapshots)
	{
		ArgumentNullException.ThrowIfNull(snapshots);
		_snapshots = snapshots;
	}

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
