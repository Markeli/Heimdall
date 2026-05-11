using Microsoft.Extensions.Logging;

namespace Heimdall.Api.Audit;

public sealed class AuditLogger
{
	private readonly ILogger _logger;

	public AuditLogger(ILoggerFactory loggerFactory)
	{
		ArgumentNullException.ThrowIfNull(loggerFactory);
		_logger = loggerFactory.CreateLogger("Heimdall.Audit");
	}

	public void Allow(
		string ecosystem, string feed, string packageId, string version,
		string remoteIp, string userAgent)
	{
		_logger.LogInformation(
			"audit.download decision={Decision} ecosystem={Ecosystem} feed={Feed} "
			+ "package={PackageId} version={Version} ip={RemoteIp} ua={UserAgent}",
			"allow", ecosystem, feed, packageId, version, remoteIp, userAgent);
	}

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
