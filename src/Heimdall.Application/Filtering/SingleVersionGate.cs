using Heimdall.Domain.Configuration;
using Heimdall.Domain.Filtering;
using Heimdall.Domain.Packages;

namespace Heimdall.Application.Filtering;

public sealed class SingleVersionGate : ISingleVersionGate
{
	private readonly IRuleEvaluator _evaluator;
	private readonly IRuleFactory _factory;

	public SingleVersionGate(IRuleEvaluator evaluator, IRuleFactory factory)
	{
		ArgumentNullException.ThrowIfNull(evaluator);
		ArgumentNullException.ThrowIfNull(factory);
		_evaluator = evaluator;
		_factory = factory;
	}

	public RuleVerdict Check(PackageVersionMetadata meta, FeedConfig feed, DateTimeOffset nowUtc)
	{
		ArgumentNullException.ThrowIfNull(meta);
		ArgumentNullException.ThrowIfNull(feed);

		var rules = _factory.BuildRules(feed.Rules);
		var ctx = new RuleContext(feed.Ecosystem, feed.Name, nowUtc);

		return _evaluator.Evaluate(meta, rules, ctx);
	}
}
