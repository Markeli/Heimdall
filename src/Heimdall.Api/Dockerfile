FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore the Cake tool first to maximise cache reuse on iterative builds.
COPY .config/dotnet-tools.json .config/
RUN dotnet tool restore

# Copy everything Cake needs to call the same Publish target the developer runs locally.
COPY global.json NuGet.config Directory.Build.props Directory.Packages.props build.cake Heimdall.sln ./
COPY src/ src/
COPY tests/ tests/

RUN dotnet cake --target=Publish --output=/app --configuration=Release

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Heimdall.Api.dll"]
