using Microsoft.Extensions.Logging;

namespace Heimdall.Api.Audit;

/// <summary>
/// Structured logger that emits one audit record per allow or deny decision taken by the proxy.
/// The output uses the dedicated <c>Heimdall.Audit</c> category and stable message templates so the
/// records can be reliably parsed downstream.
/// </summary>
public sealed class AuditLogger
{
	private readonly ILogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="AuditLogger"/> class.
	/// </summary>
	/// <param name="loggerFactory">Factory used to create the dedicated audit logger.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="loggerFactory"/> is null.</exception>
	public AuditLogger(ILoggerFactory loggerFactory)
	{
		ArgumentNullException.ThrowIfNull(loggerFactory);
		_logger = loggerFactory.CreateLogger("Heimdall.Audit");
	}

	/// <summary>
	/// Records an allow decision for a successful package download.
	/// </summary>
	/// <param name="ecosystem">Package ecosystem identifier (e.g. <c>nuget</c>).</param>
	/// <param name="feed">Logical feed name that served the request.</param>
	/// <param name="packageId">Package identifier.</param>
	/// <param name="version">Package version string as resolved upstream.</param>
	/// <param name="remoteIp">Client IP address, or <c>-</c> when unavailable.</param>
	/// <param name="userAgent">Raw <c>User-Agent</c> header value.</param>
	public void Allow(
		string ecosystem, string feed, string packageId, string version,
		string remoteIp, string userAgent)
	{
		_logger.LogInformation(
			"audit.download decision={Decision} ecosystem={Ecosystem} feed={Feed} "
			+ "package={PackageId} version={Version} ip={RemoteIp} ua={UserAgent}",
			"allow", ecosystem, feed, packageId, version, remoteIp, userAgent);
	}

	/// <summary>
	/// Records a deny decision produced by the filtering pipeline.
	/// </summary>
	/// <param name="ecosystem">Package ecosystem identifier (e.g. <c>nuget</c>).</param>
	/// <param name="feed">Logical feed name that served the request.</param>
	/// <param name="packageId">Package identifier.</param>
	/// <param name="version">Package version string as resolved upstream.</param>
	/// <param name="ruleName">Name of the rule that produced the deny verdict.</param>
	/// <param name="reason">Human-readable reason supplied by the rule.</param>
	/// <param name="remoteIp">Client IP address, or <c>-</c> when unavailable.</param>
	/// <param name="userAgent">Raw <c>User-Agent</c> header value.</param>
	public void Deny(
		string ecosystem, string feed, string packageId, string version,
		string ruleName, string reason, string remoteIp, string userAgent)
	{
		_logger.LogWarning(
			"audit.download decision={Decision} ecosystem={Ecosystem} feed={Feed} "
			+ "package={PackageId} version={Version} rule={RuleName} reason={Reason} "
			+ "ip={RemoteIp} ua={UserAgent}",
			"deny", ecosystem, feed, packageId, version, ruleName, reason, remoteIp, userAgent);
	}
}
