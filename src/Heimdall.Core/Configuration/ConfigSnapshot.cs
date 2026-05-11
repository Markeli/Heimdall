using Heimdall.Core.Configuration;

namespace Heimdall.Core.Configuration;

public sealed record ConfigSnapshot(long Generation, IReadOnlyList<FeedConfig> Feeds);
