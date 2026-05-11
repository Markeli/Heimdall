using Heimdall.Application.Configuration;
using Heimdall.Infrastructure.Configuration;
using Heimdall.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NetEscapades.Configuration.Yaml;

namespace Heimdall.IntegrationTests.Configuration;

public class YamlConfigurationReloadTests : IDisposable
{
	private readonly string _tempDir;
	private readonly string _yamlPath;

	public YamlConfigurationReloadTests()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), "heimdall-yaml-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempDir);
		_yamlPath = Path.Combine(_tempDir, "heimdall.yaml");
	}

	public void Dispose()
	{
		try
		{
			Directory.Delete(_tempDir, recursive: true);
		}
		catch
		{
			// best-effort cleanup; tests must not fail on shutdown
		}
	}

	[Fact]
	public async Task Snapshot_picks_up_yaml_changes_with_incremented_generation()
	{
		await File.WriteAllTextAsync(_yamlPath, FeedYaml(days: 14));

		var configuration = new ConfigurationBuilder()
			.SetBasePath(_tempDir)
			.AddYamlFile("heimdall.yaml", optional: false, reloadOnChange: true)
			.Build();

		var services = new ServiceCollection();
		services.AddSingleton<IConfiguration>(configuration);
		services.AddHeimdallInfrastructure(configuration);
		var sp = services.BuildServiceProvider();

		_ = sp.GetRequiredService<IOptions<HeimdallOptions>>().Value;

		var provider = sp.GetRequiredService<IConfigSnapshotProvider>();

		var initial = provider.Capture();
		initial.Feeds.Should().HaveCount(1);
		initial.Feeds[0].Rules.Single().Parameters["days"].Should().Be("14");
		var initialGeneration = initial.Generation;

		var monitor = sp.GetRequiredService<IOptionsMonitor<HeimdallOptions>>();
		var changeSignal = new TaskCompletionSource();
		using var sub = monitor.OnChange((_, _) => changeSignal.TrySetResult());

		await File.WriteAllTextAsync(_yamlPath, FeedYaml(days: 30));
		File.SetLastWriteTimeUtc(_yamlPath, DateTime.UtcNow);

		var fired = await Task.WhenAny(changeSignal.Task, Task.Delay(TimeSpan.FromSeconds(5)));
		fired.Should().Be(changeSignal.Task, "OnChange should fire when YAML changes");

		var updated = provider.Capture();
		updated.Feeds[0].Rules.Single().Parameters["days"].Should().Be("30");
		updated.Generation.Should().BeGreaterThan(initialGeneration);
	}

	private static string FeedYaml(int days) =>
		$$"""
		heimdall:
		  server:
		    listen: "http://0.0.0.0:8080"
		    publicBaseUrl: "https://heimdall.local"
		  ecosystems:
		    nuget:
		      feeds:
		        - name: strict
		          upstream: "https://api.nuget.org/v3/index.json"
		          rules:
		            - type: minAgeDays
		              days: "{{days}}"
		""";
}
