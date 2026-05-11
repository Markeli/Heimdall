using Heimdall.Core.Configuration;
using Heimdall.Core.Filtering;
using Heimdall.Core.Packages;

namespace Heimdall.Core.Filtering;

/// <summary>
/// Default <see cref="IVersionListFilter"/>. Materialises the feed's rules once and applies
/// them to every candidate version, keeping only the ones that pass.
/// </summary>
public sealed class VersionListFilter : IVersionListFilter
{
	private readonly IRuleEvaluator _evaluator;
	private readonly IRuleFactory _factory;

	/// <summary>
	/// Creates a new <see cref="VersionListFilter"/>.
	/// </summary>
	/// <param name="evaluator">Rule evaluator used to compute per-version verdicts.</param>
	/// <param name="factory">Factory used to materialise the feed's rules.</param>
	/// <exception cref="ArgumentNullException">A required dependency is <c>null</c>.</exception>
	public VersionListFilter(IRuleEvaluator evaluator, IRuleFactory factory)
	{
		ArgumentNullException.ThrowIfNull(evaluator);
		ArgumentNullException.ThrowIfNull(factory);
		_evaluator = evaluator;
		_factory = factory;
	}

	/// <inheritdoc />
	/// <exception cref="ArgumentNullException"><paramref name="metas"/> or <paramref name="feed"/> is <c>null</c>.</exception>
	public IReadOnlyList<PackageVersionMetadata> Apply(
		IEnumerable<PackageVersionMetadata> metas,
		FeedConfig feed,
		DateTimeOffset nowUtc)
	{
		ArgumentNullException.ThrowIfNull(metas);
		ArgumentNullException.ThrowIfNull(feed);

		var rules = _factory.BuildRules(feed.Rules);
		var ctx = new RuleContext(feed.Ecosystem, feed.Name, nowUtc);

		var passed = new List<PackageVersionMetadata>();
		foreach (var meta in metas)
		{
			var verdict = _evaluator.Evaluate(meta, rules, ctx);
			if (verdict.Decision == FilterDecision.Allow)
			{
				passed.Add(meta);
			}
		}

		return passed;
	}
}
