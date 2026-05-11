using Heimdall.Core.Configuration;
using Heimdall.Core.Filtering;
using Heimdall.Core.Packages;

namespace Heimdall.Core.Filtering;

public sealed class VersionListFilter : IVersionListFilter
{
	private readonly IRuleEvaluator _evaluator;
	private readonly IRuleFactory _factory;

	public VersionListFilter(IRuleEvaluator evaluator, IRuleFactory factory)
	{
		ArgumentNullException.ThrowIfNull(evaluator);
		ArgumentNullException.ThrowIfNull(factory);
		_evaluator = evaluator;
		_factory = factory;
	}

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
