namespace Heimdall.Core.Filtering;

/// <summary>
/// Binary filter outcome for a single package version.
/// </summary>
public enum FilterDecision
{
	/// <summary>The version is permitted to flow through.</summary>
	Allow,

	/// <summary>The version is blocked.</summary>
	Deny,
}
