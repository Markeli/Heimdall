using Heimdall.Domain.Configuration;

namespace Heimdall.Application.Configuration;

public sealed record ConfigSnapshot(long Generation, IReadOnlyList<FeedConfig> Feeds);
