// Heimdall build script. Run from the repository root:
//   dotnet tool restore
//   dotnet cake --target=Publish [--output=...] [--configuration=Release]
//
// Local invocations and the Dockerfile both call into this script so artifacts are bit-identical.

var target = Argument("target", "Build");
var configuration = Argument("configuration", "Release");
var output = Argument("output", "./artifacts/publish");

var solution = "./Heimdall.sln";
var apiProject = "./src/Heimdall.Api/Heimdall.Api.csproj";

Task("Restore")
    .Does(() =>
{
    DotNetRestore(solution);
});

Task("Build")
    .IsDependentOn("Restore")
    .Does(() =>
{
    DotNetBuild(solution, new DotNetBuildSettings
    {
        Configuration = configuration,
        NoRestore = true,
    });
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    DotNetTest(solution, new DotNetTestSettings
    {
        Configuration = configuration,
        NoBuild = true,
    });
});

Task("Publish")
    .IsDependentOn("Build")
    .Does(() =>
{
    DotNetPublish(apiProject, new DotNetPublishSettings
    {
        Configuration = configuration,
        OutputDirectory = output,
        NoBuild = true,
        MSBuildSettings = new DotNetMSBuildSettings().WithProperty("UseAppHost", "false"),
    });
});

RunTarget(target);
