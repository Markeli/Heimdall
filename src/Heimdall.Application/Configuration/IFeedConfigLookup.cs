using System.Diagnostics.CodeAnalysis;
using Heimdall.Domain.Configuration;

namespace Heimdall.Application.Configuration;

public interface IFeedConfigLookup
{
	bool TryGet(string ecosystem, string feedName, [NotNullWhen(true)] out FeedConfig? config);
}
