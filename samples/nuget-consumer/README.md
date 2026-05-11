# nuget-consumer sample

A minimal `dotnet` project used by Heimdall's smoke tests to verify that a real
NuGet client can resolve and restore packages **through** a running Heimdall
instance. The `NuGet.config` points exclusively at the `relaxed` feed on
`http://localhost:8080` and clears the default sources; the project then
references `Newtonsoft.Json` so a normal `dotnet restore` must traverse the
proxy.

The release pipeline runs the following against the container under test:

```bash
dotnet restore samples/nuget-consumer/Consumer.csproj \
  --configfile samples/nuget-consumer/NuGet.config \
  --packages /tmp/heimdall-smoke-packages
```

The `--packages` flag isolates the restore from the runner's global package
cache so the bytes definitely traverse Heimdall.
