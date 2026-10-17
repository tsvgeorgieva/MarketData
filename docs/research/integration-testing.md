# Integration testing the ingester and API (.NET 10)

Issue: tsvgeorgieva/MarketData#13. Scope: Testcontainers for .NET with SQL Server, `WebApplicationFactory`, faking the Binance WebSocket, and test-time clock control. Primary sources only; each claim cites its source. Anything I could not confirm is listed under "Unverified / open".

## Summary / recommendation

1. **SQL Server**: use `Testcontainers.MsSql` (real SQL Server in Docker), one container per test class or per assembly via an xUnit fixture. Do not use the EF Core InMemory provider; the ASP.NET docs steer away from it.
2. **API**: `WebApplicationFactory<Program>` with `ConfigureWebHost` to swap the DbContext connection string and other services.
3. **Binance WebSocket**: put the connection behind a small abstraction you own (for example `IMarketDataStream`) for most tests. For one or two true integration tests, run a fake Binance server on real Kestrel (`UseKestrel`, new in .NET 10) so the real `ClientWebSocket` code path is exercised. `TestServer`'s in-memory `WebSocketClient` is not a `ClientWebSocket`, so it cannot exercise code that news up `ClientWebSocket`.
4. **Clock**: inject `TimeProvider` (built into .NET 8+) and use `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing` in tests.

## 1. Testcontainers for .NET with SQL Server

- Package `Testcontainers.MsSql`; container built with `MsSqlBuilder`, started with `StartAsync()`. The docs example passes an explicit image, `mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04`, and does not state a default image. [MSSQL module](https://dotnet.testcontainers.org/modules/mssql/)
- Connection string: `GetConnectionString()`; the example also uses `GetConnectionString(ConnectionMode.Host)`. The page does not explain `ConnectionMode`. The page notes the string works with EF Core, so migrations can be run against it. [MSSQL module](https://dotnet.testcontainers.org/modules/mssql/)
- Example test deps: `Microsoft.Data.SqlClient`, xunit.v3, Microsoft.NET.Test.Sdk. [MSSQL module](https://dotnet.testcontainers.org/modules/mssql/)
- Builders are immutable (each call returns a new builder). `StartAsync` takes a `CancellationToken`. `WithReuse` exists but is marked experimental. `GetLogsAsync` / `WithOutputConsumer` help diagnose startup. [Create a container](https://dotnet.testcontainers.org/api/create_docker_container/)
- Cleanup: the Resource Reaper (Moby Ryuk) removes leftover resources after the run, pass or fail; the docs advise against disabling it. Only Linux containers are supported, so Windows dev machines need Docker in Linux-container mode (Docker Desktop / WSL2). [Resource Reaper](https://dotnet.testcontainers.org/api/resource_reaper/)
- xUnit lifecycle patterns documented: per-test isolated container (`ContainerTest<,>`), per-class shared container (`ContainerFixture<,>` + `IClassFixture<T>`), and assembly-wide (`[assembly: AssemblyFixture(typeof(...))]`, xUnit v3 or a compatible v2 extension). Pin image versions rather than `latest`. [xUnit.net](https://dotnet.testcontainers.org/test_frameworks/xunit_net/)
- Trade-off (my analysis): a container per test is isolated but slow; a shared container needs per-test data cleanup. The docs describe both and do not prescribe one.

## 2. WebApplicationFactory

- `WebApplicationFactory<TEntryPoint>` (package `Microsoft.AspNetCore.Mvc.Testing`) bootstraps the app in an in-memory `TestServer`; `CreateClient()` returns an `HttpClient`. [ASP.NET Core integration tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
- Customise by subclassing and overriding `ConfigureWebHost`; for generic-host apps the test `ConfigureServices` runs after the app's `Program.cs` registrations, so it can replace them. Per-test overrides: `WithWebHostBuilder(b => b.ConfigureTestServices(...))`. [same](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
- Replacing the DbContext: remove the existing `IDbContextOptionsConfiguration<TContext>` descriptor (and any `DbConnection` descriptor) and re-register. The doc's sample targets SQLite in-memory; it says that for SQL Server you reference `Microsoft.EntityFrameworkCore.SqlServer` and call `UseSqlServer` with a test connection string. The doc recommends SQLite in-memory over the EF Core InMemory provider. [same](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
- Seeding: resolve the context from `_factory.Services.CreateScope()`. [same](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
- Lifecycle: `IClassFixture<WebApplicationFactory<Program>>` shares one factory per test class and disposes it after. [same](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
- For feeding in the container's connection string, `ConfigureWebHost` and `ConfigureWebApplicationBuilder(IHostApplicationBuilder)` exist; the latter "will be called very early, during the entrypoint's call to WebApplication.CreateBuilder". [WebApplicationFactory API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.testing.webapplicationfactory-1?view=aspnetcore-10.0)
- **.NET 10 adds `UseKestrel()`, `UseKestrel(int port)` (0 = dynamic port) and `UseKestrel(Action<KestrelServerOptions>)`** to configure the factory to use Kestrel as the server. It must be called before the factory is initialised (via `CreateClient()` or `StartServer()`), else `InvalidOperationException`. Listed only for Mvc.Testing 10.0.0+. [UseKestrel](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.testing.webapplicationfactory-1.usekestrel?view=aspnetcore-10.0)
- In 10, `TestServer(IWebHostBuilder)` and `WebApplicationFactory.CreateServer(IWebHostBuilder)` are obsolete; prefer the generic host (`WebApplication`). [TestServer](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.testhost.testserver?view=aspnetcore-10.0)

## 3. Faking the Binance WebSocket

Facts about the real endpoint ([Binance Spot WebSocket Streams](https://developers.binance.com/docs/binance-spot-api-docs/web-socket-streams)):
- Base endpoints include `wss://stream.binance.com:9443`, `:443`, and a testnet `wss://stream.testnet.binance.vision`. Raw streams: `/ws/<symbol>@trade`; combined: `/stream?streams=...`, or connect then send a `SUBSCRIBE` message with `method`, `params`, `id`.
- Trade payload fields: `e, E, s, t, p, q, T, m, M`. Kline payload: `e, E, s, k{t,T,s,i,f,L,o,c,h,l,v,n,x,q,V,Q,B}`. Use these as fixtures.

Facts about the .NET pieces:
- `ClientWebSocket` is `sealed`. Exactly one concurrent send and one concurrent receive are supported per instance. It has a `ConnectAsync(Uri, HttpMessageInvoker, CancellationToken)` overload. [ClientWebSocket](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.clientwebsocket?view=net-10.0)
- `TestServer.CreateWebSocketClient()` returns a `Microsoft.AspNetCore.TestHost.WebSocketClient` with `ConnectAsync(Uri, CancellationToken)`, `SubProtocols`, `ConfigureRequest`. It is a separate test-host type, not a `ClientWebSocket`. [TestServer](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.testhost.testserver?view=aspnetcore-10.0), [WebSocketClient](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.testhost.websocketclient?view=aspnetcore-10.0)
- Server side: ASP.NET Core WebSocket middleware (`UseWebSockets`, `AcceptWebSocketAsync`) is how a fake Binance endpoint would be written. [WebSockets in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-10.0)

Options (my analysis built on the above, not claims of the docs):

| Option | Exercises real `ClientWebSocket` | Cost | Use for |
|---|---|---|---|
| A. Abstraction (`IMarketDataStream` yielding parsed messages; fake yields a scripted sequence) | No | Lowest; deterministic | Ingester logic: parsing to domain, batching, persistence, error handling |
| B. Fake Binance server on its own host with real Kestrel (`UseKestrel(0)` or a minimal `WebApplication` on port 0); ingester pointed at `ws://localhost:<port>/ws/...` | Yes | Medium; needs a configurable base URL | Connect/reconnect, close handshake, fragmented frames, server-initiated drops |
| C. `TestServer.CreateWebSocketClient()` | No (different client type) | Low | Only for testing your own WebSocket server endpoints, not the ingester client |

Recommendation: A for the bulk, B for a thin set of connection-lifecycle tests. Make the Binance base URL configuration (`IOptions`) so B only needs a config override.

## 4. BackgroundService under WebApplicationFactory

- The integration-test article, as read, does not discuss hosted-service behaviour under `TestServer` (I read only the first ~100k of ~267k characters). Treat "do hosted services start with the factory?" as something to verify in a spike. [integration tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
- Documented lever: services can be replaced or removed in `ConfigureTestServices`, so API tests can drop the real ingester hosted service, and ingester tests can run it against the fake server.

## 5. Clock control

- `TimeProvider` is in the framework from .NET 8; `TimeProvider.System` is the default. It provides `GetUtcNow`, `GetTimestamp`, `CreateTimer`. `Task.Delay(TimeSpan, TimeProvider)`, `Task.WaitAsync(..., TimeProvider)` and `new CancellationTokenSource(TimeSpan, TimeProvider)` accept it. [TimeProvider overview](https://learn.microsoft.com/en-us/dotnet/standard/datetime/timeprovider-overview)
- `FakeTimeProvider` (package `Microsoft.Extensions.TimeProvider.Testing`, v10.10.0 listed): ctor with a start `DateTimeOffset`; `Advance(TimeSpan)`, `SetUtcNow`, `AutoAdvanceAmount`, `SetLocalTimeZone`, `CreateTimer`. "Advancing time affects the timers created from this provider, and all other operations that are directly or indirectly using this provider as a time source"; the app must call `Advance` explicitly. [FakeTimeProvider](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.time.testing.faketimeprovider?view=net-10.0-pp), [Advance](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.time.testing.faketimeprovider.advance?view=net-10.0-pp)
- Wiring (my analysis): register `TimeProvider.System` in production; in the factory's `ConfigureTestServices`, replace the `TimeProvider` descriptor with a shared `FakeTimeProvider` instance the test also holds. Reconnect backoff and flush timers must use `timeProvider.Delay(...)` / `CreateTimer` (not `Task.Delay(TimeSpan)` or `DateTime.UtcNow`) for this to work.
- Caveat: the fake clock does not affect SQL Server's own `SYSUTCDATETIME()` or column defaults; set timestamps from application code.

## Proposed test layout

- `Tests.Integration` project: `SqlServerFixture` (assembly or collection fixture, `MsSqlBuilder` with pinned image), `ApiFactory : WebApplicationFactory<Program>` (container connection string + `FakeTimeProvider` + ingester removed), `IngesterFactory` (ingester + fake Binance server on Kestrel port 0 + `FakeTimeProvider`).
- Per-test data isolation is not prescribed by the sources; pick one (respawn tool, transaction rollback, or truncate in `InitializeAsync`) in a follow-up.

## Unverified / open

- Binance connection limits (24h connection lifetime, ping/pong interval, message-rate limits): the page section was not retrievable in this pass; read "General WSS information" before designing reconnect logic.
- Whether `IHostedService`s start under `WebApplicationFactory` / `TestServer`, and interplay with `UseKestrel`: not covered in the portions read; spike it.
- Whether `ClientWebSocket.ConnectAsync(Uri, HttpMessageInvoker, ...)` can be driven by the `TestServer` handler: not documented in the sources read; I do not rely on it.
- Testcontainers `MsSql` default image, EULA acceptance, wait strategy: not on the module page; check module source if it matters.
- SQL Server image on ARM hosts and Windows-container hosts: not researched.
