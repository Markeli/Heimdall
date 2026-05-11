using System.Diagnostics.CodeAnalysis;
using Heimdall.Core.Configuration;

namespace Heimdall.Core.Configuration;

/// <summary>
/// Resolves a feed configuration by ecosystem and feed name.
/// </summary>
public interface IFeedConfigLookup
{
	/// <summary>
	/// Attempts to resolve the configuration of a single feed.
	/// </summary>
	/// <param name="ecosystem">Ecosystem identifier (e.g. <c>nuget</c>).</param>
	/// <param name="feedName">Logical feed name.</param>
	/// <param name="config">When the method returns <c>true</c>, contains the matching configuration.</param>
	/// <returns><c>true</c> when a matching feed exists; otherwise <c>false</c>.</returns>
	bool TryGet(string ecosystem, string feedName, [NotNullWhen(true)] out FeedConfig? config);
}
