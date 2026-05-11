namespace Heimdall.Core.Configuration;

public interface IConfigSnapshotProvider
{
	ConfigSnapshot Capture();
}
