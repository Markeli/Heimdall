using Heimdall.Core.Configuration;
using Heimdall.Core.Filtering;
using Heimdall.Core.Packages;

namespace Heimdall.Core.Filtering;

/// <summary>
/// Default <see cref="ISingleVersionGate"/>. Materialises the feed's rules via
/// <see cref="IRuleFactory"/> and delegates evaluation to <see cref="IRuleEvaluator"/>.
/// </summary>
public sealed class SingleVersionGate : ISingleVersionGate
{
	private readonly IRuleEvaluator _evaluator;
	private readonly IRuleFactory _factory;

	/// <summary>
	/// Creates a new <see cref="SingleVersionGate"/>.
	/// </summary>
	/// <param name="evaluator">Rule evaluator used to compute the verdict.</param>
	/// <param name="factory">Factory used to materialise the feed's rules.</param>
	/// <exception cref="ArgumentNullException">A required dependency is <c>null</c>.</exception>
	public SingleVersionGate(IRuleEvaluator evaluator, IRuleFactory factory)
	{
		ArgumentNullException.ThrowIfNull(evaluator);
		ArgumentNullException.ThrowIfNull(factory);
		_evaluator = evaluator;
		_factory = factory;
	}

	/// <inheritdoc />
	/// <exception cref="ArgumentNullException"><paramref name="meta"/> or <paramref name="feed"/> is <c>null</c>.</exception>
	public RuleVerdict Check(PackageVersionMetadata meta, FeedConfig feed, DateTimeOffset nowUtc)
	{
		ArgumentNullException.ThrowIfNull(meta);
		ArgumentNullException.ThrowIfNull(feed);

		var rules = _factory.BuildRules(feed.Rules);
		var ctx = new RuleContext(feed.Ecosystem, feed.Name, nowUtc);

		return _evaluator.Evaluate(meta, rules, ctx);
	}
}
