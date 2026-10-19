# MarketData

Market data collection and analysis service built with .NET 10. It collects closed 1-minute candles from the Binance WebSocket stream, stores them in SQL Server and serves the 24-hour average price and the Simple Moving Average over an HTTP API (JSON or XML). A small console client calls that API.

## Documentation

| Document | What it covers |
| --- | --- |
| [docs/engineering-notes.md](docs/engineering-notes.md) | What was not completed, how AI was used, the instructions given to it, how its output was verified and where its proposals were overruled. **Start here** |
| [Specification (issue #18)](https://github.com/tsvgeorgieva/MarketData/issues/18) | Business decisions, technical design and architecture, story planning, testing decisions and future improvements |
| [docs/deployment-design.md](docs/deployment-design.md) | Proposed Azure deployment and CI/CD pipeline, with the reasoning behind each choice |
| [docs/GLOSSARY.md](docs/GLOSSARY.md) | The vocabulary used in the code and the spec |
| [docs/primer-market-data.md](docs/primer-market-data.md) | Plain-language background on candles and moving averages |

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) with Compose, to run the system. The SQL Server image is large and amd64-only.
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (version in [global.json](global.json)), to run the tests and the console client.
- Internet access, because the service reads live data from Binance.

## Start the system

```bash
docker compose up --build
```

This starts SQL Server and the API on <http://localhost:8080>. The database is created and migrated automatically, and the symbols `BTCUSDT`, `ADAUSDT` and `ETHUSDT` are monitored from the first start. The first candles arrive within a minute, because only closed 1-minute candles are stored.

To check that it is up, or to run the same check as a script (it starts the system and waits for the health endpoint; pass `--down` to stop and clean up afterwards):

```bash
curl http://localhost:8080/health/live
bash scripts/smoke-test.sh
```

To stop it, and to also delete the stored data:

```bash
docker compose down
docker compose down -v
```

### Configuration

The compose file carries throwaway values for local use, so nothing has to be set. To change them, copy [.env.example](.env.example) to `.env` (git-ignored):

| Variable | Default | Meaning |
| --- | --- | --- |
| `API_PORT` | `8080` | Port the API is published on |
| `ADMIN_API_KEY` | `local-dev-only-admin-key` | Key for the admin endpoints (at least 16 characters) |
| `SQL_SA_PASSWORD` | `Local-dev-only-Passw0rd` | SQL Server `sa` password |

## Try the API

The examples assume the default port and key. The interactive Swagger UI is at <http://localhost:8080/swagger>, where every endpoint can be tried from the browser. For the admin endpoints, click **Authorize** and enter the admin key. The machine-readable API description it reads is at <http://localhost:8080/openapi/v1.json>.

**24-hour average price** (JSON by default, XML when asked):

```bash
curl http://localhost:8080/api/BTCUSDT/24hAvgPrice
curl -H "Accept: application/xml" http://localhost:8080/api/BTCUSDT/24hAvgPrice
```

**Simple Moving Average**: `n` is the number of periods (1 to 1000), `p` the period (`1m`, `5m`, `30m`, `1d` or `1w`), and the optional `e` is the end of the window (UTC time without an offset, defaults to now):

```bash
curl "http://localhost:8080/api/BTCUSDT/SimpleMovingAverage?n=5&p=1m"
curl "http://localhost:8080/api/BTCUSDT/SimpleMovingAverage?n=10&p=5m&e=2026-01-01T12:00:00"
```

A `404` means no data has been collected yet for that symbol or window. Wait a minute after the first start. Errors are returned as `application/problem+json`.

**Symbol management** needs the `X-Api-Key` header. Changes apply to the live Binance connection without a restart, and a new symbol gets the last 24 hours backfilled:

```bash
curl -H "X-Api-Key: local-dev-only-admin-key" http://localhost:8080/api/admin/symbols
curl -X PUT    -H "X-Api-Key: local-dev-only-admin-key" http://localhost:8080/api/admin/symbols/SOLUSDT
curl -X DELETE -H "X-Api-Key: local-dev-only-admin-key" http://localhost:8080/api/admin/symbols/SOLUSDT
```

## Use the console client

The client needs the system to be running. It reads the API address from `--url`, then `MARKETDATA_URL`, then `http://localhost:8080`.

```bash
dotnet run --project src/MarketData.Client -- avg24h BTCUSDT
dotnet run --project src/MarketData.Client -- sma BTCUSDT -n 10 -p 5m
dotnet run --project src/MarketData.Client -- sma BTCUSDT -n 10 -p 5m --format json
```

`--format` is `text` (default), `json` or `xml`. Exit codes: `0` ok, `1` the API returned an error, `2` bad usage, `3` the API is unreachable.

## Run the tests

```bash
dotnet test
```

- The **unit tests** (`tests/MarketData.UnitTests`) need nothing else.
- The **integration tests** (`tests/MarketData.IntegrationTests`) start a real SQL Server in a container through Testcontainers, so **Docker must be running**. They fail, rather than skip, when it is not.

To run only one project:

```bash
dotnet test tests/MarketData.UnitTests
dotnet test tests/MarketData.IntegrationTests
```

Note that an integration test that checks the trace id of an error response occasionally fails and passes on rerun (see [docs/engineering-notes.md](docs/engineering-notes.md)), and that the unit tests were AI-generated and have not been reviewed yet. See [docs/engineering-notes.md](docs/engineering-notes.md).

## Assumptions, departures and known limits

The reasoning behind each is in the [specification](https://github.com/tsvgeorgieva/MarketData/issues/18).

**Departures from the assessment PDF**

- **`e` instead of `s`.** The PDF calls `s` the "starting" date and time, yet says that when it is omitted the current time is the reference point. A window starting at "now" would have no data, so the parameter is the **end** of a trailing window and is named `e`. To be confirmed with the business owner in a real project.
- **Symbol management, authentication.** The PDF asks for administrative endpoints only. They require a static `X-Api-Key` (at least 16 characters, no default), because open admin endpoints would be unsafe.

**Assumptions**

- **One price:** the close of a closed 1-minute candle. Only closed candles are stored and nothing is ever invented: no data gives a `404`, never a zero.
- **Time is UTC.** A day starts at 00:00 UTC and a week on Monday at 00:00 UTC. The newest Simple Moving Average period may still be forming; it is included and flagged `inProgress`.
- **24-hour average:** the mean over the last 24 hours of stored candles, or of fewer when less has been collected. The response reports the window actually covered and the number of data points. The database cuts the mean off at eight decimal places instead of rounding it.
- **Symbol cap:** at most 20 monitored symbols (`Symbols:MaxMonitored`; the 21st add returns `409`). At 20 symbols the data grows by about 10.5 million rows, roughly 1 GB, a year.
- **Removing a symbol keeps its history**; a re-add resumes collection.
- **Single instance.** Run one replica: two ingesters would duplicate collection.

**Known limits (future improvements in the specification)**

- **No automatic reconnect.** If the Binance connection drops (Binance closes every connection after about 24 hours), collection stops until the service is restarted. Minutes missed while the connection is down or the service is stopped stay a gap, and a restart does not backfill them.
- **Backfill of an added symbol runs inside the `PUT` request**, so the call can wait several seconds when Binance is slow. If the history request fails, the symbol is still added and its last 24 hours stay a gap.
Everything else that was not completed or not reviewed is listed in [docs/engineering-notes.md](docs/engineering-notes.md).

## Repository layout

| Path | Contents |
| --- | --- |
| `src/MarketData.Domain`, `Application`, `Infrastructure` | Clean-architecture layers; references point inward only |
| `src/MarketData.Api`, `Api.Contracts` | The ASP.NET Core host (API and ingestion) and the response DTOs shared with the client |
| `src/MarketData.Client` | The console client |
| `tests/` | Unit and integration tests |
| `scripts/` | The smoke test |
