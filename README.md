# Heimdall

Внутренний прокси для публичных пакетных репозиториев (NuGet — MVP, npm/Maven позже) с правилами фильтрации (минимальный возраст версии, allow/deny по имени пакета). Файлы пакетов проксируются стримом, метаданные кэшируются в памяти.

## Стек

- .NET 10, ASP.NET Core MVC controllers
- HttpClient + Polly (`Microsoft.Extensions.Http.Resilience`) для upstream
- Serilog → JSON в stdout
- prometheus-net → `/metrics`
- xUnit + WireMock.Net для тестов
- Конфиг — YAML с hot-reload

## Структура

```
src/
  Heimdall.Domain            # модели, контракты
  Heimdall.Application       # use-cases, фильтры, правила
  Heimdall.Infrastructure    # cache, YAML конфиг, generation
  Heimdall.Ecosystems.NuGet  # NuGet V3 specifics
  Heimdall.Api               # ASP.NET Core host, controllers
tests/
  Heimdall.UnitTests
  Heimdall.IntegrationTests  # WebApplicationFactory + WireMock.Net
```

## Запуск локально

```sh
dotnet run --project src/Heimdall.Api
```

Сервер слушает `http://localhost:8080`. Конфигурация лежит рядом с `Heimdall.Api.dll` (`heimdall.yaml`). Hot-reload при изменении файла.

## Подключение клиента

```sh
dotnet nuget add source http://localhost:8080/nuget/strict/v3/index.json -n heimdall-strict
dotnet add package Newtonsoft.Json
```

Версии моложе `minAgeDays` будут отфильтрованы из `flatcontainer/{id}/index.json`. Попытка скачать запрещённую версию вернёт `403 ProblemDetails` с указанием правила.

## Конфигурация (YAML)

```yaml
heimdall:
  server:
    publicBaseUrl: "http://localhost:8080"   # обязательное — для rewrite @id URL
  ecosystems:
    nuget:
      feeds:
        - name: strict
          upstream: "https://api.nuget.org/v3/index.json"
          cacheTtl: "00:10:00"
          rules:
            - type: minAgeDays
              days: "14"
            - type: allowDeny
              patterns: "Microsoft.*;System.*;!Internal.*"
```

### Семантика `allowDeny`
- Glob (`*`, `?`), case-insensitive. Префикс `!` — deny-pattern.
- Любой матч deny → версия отклонена (deny wins).
- Если есть хоть один allow-паттерн — пакет должен совпасть хотя бы с одним, иначе deny.
- Только deny → разрешено всё, кроме denied.

### Семантика `minAgeDays`
- Версия разрешена, если `now - catalogEntry.published >= days`.
- `published == null` → deny (страховка против повреждённых/unlisted метаданных).

## Endpoint'ы

| Path | Что делает |
|---|---|
| `GET /nuget/{feed}/v3/index.json` | Service index (URL'ы указывают на Heimdall) |
| `GET /nuget/{feed}/v3/flatcontainer/{id}/index.json` | Список разрешённых версий |
| `GET /nuget/{feed}/v3/registration5-gz-semver2/{id}/index.json` | Registration с фильтром + URL rewrite |
| `GET /nuget/{feed}/v3/query?q=...` | Search с фильтром |
| `GET\|HEAD /nuget/{feed}/v3/flatcontainer/{id}/{ver}/{file}.nupkg` | Download через gate + stream |
| `GET /healthz` | Liveness (200 ok) |
| `GET /readyz` | Readiness (проверяет upstream reachability) |
| `GET /metrics` | Prometheus метрики |

## Тесты

```sh
dotnet test
```

- 42 unit-теста: правила, фильтры, кэш, конфиг, transformer, URL rewriter.
- 9 integration-тестов: WebApplicationFactory + WireMock.Net (service-index, listing, download allow/deny, health, hot-reload).

## Docker

```sh
docker build -f src/Heimdall.Api/Dockerfile -t heimdall:dev .
docker run --rm -p 8080:8080 -v $(pwd)/heimdall.yaml:/app/heimdall.yaml heimdall:dev
```

## Out of scope (MVP)

- L2 Redis (контракт готов, регистрируется через DI).
- npm/Maven экосистемы.
- CVE/vulnerability rules.
- Auth (анонимный доступ внутри контура).
- Multi-instance scaling.

См. `/Users/markelow/.claude/plans/expressive-skipping-beacon.md` — финальный архитектурный план.
