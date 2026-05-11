using System.Diagnostics.CodeAnalysis;
using Heimdall.Core.Configuration;

namespace Heimdall.Core.Configuration;

public interface IFeedConfigLookup
{
	bool TryGet(string ecosystem, string feedName, [NotNullWhen(true)] out FeedConfig? config);
}
