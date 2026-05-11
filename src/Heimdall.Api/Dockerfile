FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json NuGet.config Directory.Build.props Directory.Packages.props ./
COPY src/Heimdall.Domain/Heimdall.Domain.csproj src/Heimdall.Domain/
COPY src/Heimdall.Application/Heimdall.Application.csproj src/Heimdall.Application/
COPY src/Heimdall.Infrastructure/Heimdall.Infrastructure.csproj src/Heimdall.Infrastructure/
COPY src/Heimdall.Ecosystems.NuGet/Heimdall.Ecosystems.NuGet.csproj src/Heimdall.Ecosystems.NuGet/
COPY src/Heimdall.Api/Heimdall.Api.csproj src/Heimdall.Api/
RUN dotnet restore src/Heimdall.Api/Heimdall.Api.csproj

COPY src/ src/
RUN dotnet publish src/Heimdall.Api/Heimdall.Api.csproj \
    -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Heimdall.Api.dll"]
