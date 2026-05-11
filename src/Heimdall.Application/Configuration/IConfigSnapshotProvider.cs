namespace Heimdall.Application.Configuration;

public interface IConfigSnapshotProvider
{
	ConfigSnapshot Capture();
}
