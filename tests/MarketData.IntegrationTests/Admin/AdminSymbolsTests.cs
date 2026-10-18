using System.Net;
using System.Net.Http.Json;
using System.Xml.Linq;
using MarketData.Api.Admin;
using MarketData.Api.Contracts;
using MarketData.Domain.Candles;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;
using MarketData.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarketData.IntegrationTests.Admin;

public class AdminSymbolsTests(SqlServerFixture sql)
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key-0123456789")]
    [InlineData("")]
    public async Task Admin_MissingOrWrongKey_ReturnsUnauthorizedForListAndRemove(string? key)
    {
        await using var factory = NewFactory(out _);
        using var client = factory.CreateClient();
        if (key is not null)
        {
            client.DefaultRequestHeaders.Add(AdminApiKeyAttribute.HeaderName, key);
        }

        var list = await client.GetAsync("/api/admin/symbols", TestContext.Current.CancellationToken);
        var remove = await client.DeleteAsync("/api/admin/symbols/BTCUSDT", TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (list.StatusCode, remove.StatusCode));
    }

    [Fact]
    public async Task Remove_WithoutKey_DoesNotRemoveTheSymbol()
    {
        await using var factory = NewFactory(out var connectionString);
        using var client = factory.CreateClient();

        await client.DeleteAsync("/api/admin/symbols/BTCUSDT", TestContext.Current.CancellationToken);

        Assert.Equal(0, await RemovedCountAsync(connectionString, "BTCUSDT"));
    }

    [Fact]
    public async Task List_ReturnsTheMonitoredSymbolsWithTheirAddedTimeInAlphabeticalOrder()
    {
        await using var factory = NewFactory(out _);
        using var client = AdminClient(factory);

        var response = await client.GetFromJsonAsync<MonitoredSymbolsResponse>("/api/admin/symbols", TestContext.Current.CancellationToken);

        Assert.Equal(
            [("ADAUSDT", Now.UtcDateTime), ("BTCUSDT", Now.UtcDateTime), ("ETHUSDT", Now.UtcDateTime)],
            response!.Symbols.Select(symbol => (symbol.Symbol, symbol.AddedAt)));
    }

    [Fact]
    public async Task List_AcceptXml_ReturnsTheNamedWrapper()
    {
        await using var factory = NewFactory(out _);
        using var client = AdminClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/symbols");
        request.Headers.Add("Accept", "application/xml");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var root = XElement.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            ("monitoredSymbols", "ADAUSDT", "2026-01-02T12:00:00Z"),
            (root.Name.LocalName, root.Element("symbols")!.Elements("symbol").First().Element("symbol")!.Value,
                root.Element("symbols")!.Elements("symbol").First().Element("addedAt")!.Value));
    }

    [Fact]
    public async Task Remove_MonitoredSymbol_ReturnsNoContentAndDropsItFromTheListAndTheReadEndpoints()
    {
        await using var factory = NewFactory(out _);
        using var client = AdminClient(factory);

        var response = await client.DeleteAsync("/api/admin/symbols/btcusdt", TestContext.Current.CancellationToken);

        var list = await client.GetFromJsonAsync<MonitoredSymbolsResponse>("/api/admin/symbols", TestContext.Current.CancellationToken);
        var read = await client.GetAsync("/api/BTCUSDT/24hAvgPrice", TestContext.Current.CancellationToken);
        Assert.Equal(
            (HttpStatusCode.NoContent, "ADAUSDT,ETHUSDT", HttpStatusCode.NotFound),
            (response.StatusCode, string.Join(',', list!.Symbols.Select(symbol => symbol.Symbol)), read.StatusCode));
    }

    [Fact]
    public async Task Remove_KeepsTheStoredCandles()
    {
        await using var factory = NewFactory(out var connectionString);
        using var client = AdminClient(factory);
        await factory.Services.GetRequiredService<ICandleRepository>()
            .InsertMissingAsync([Candle("BTCUSDT", minute: 0, close: 100m)], TestContext.Current.CancellationToken);

        await client.DeleteAsync("/api/admin/symbols/BTCUSDT", TestContext.Current.CancellationToken);

        Assert.Equal(1, await TestDatabase.ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM Candles WHERE Symbol = 'BTCUSDT'"));
    }

    [Theory]
    [InlineData("XRPUSDT")]
    [InlineData("BTCUSDT")]
    public async Task Remove_UnknownOrAlreadyRemovedSymbol_ReturnsNoContent(string symbol)
    {
        await using var factory = NewFactory(out _);
        using var client = AdminClient(factory);
        await client.DeleteAsync("/api/admin/symbols/BTCUSDT", TestContext.Current.CancellationToken);

        var response = await client.DeleteAsync($"/api/admin/symbols/{symbol}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Remove_SymbolRemovedTwice_KeepsTheFirstRemovalTime()
    {
        await using var factory = NewFactory(out var connectionString);
        using var client = AdminClient(factory);
        await client.DeleteAsync("/api/admin/symbols/BTCUSDT", TestContext.Current.CancellationToken);
        factory.Clock.Advance(TimeSpan.FromHours(1));

        await client.DeleteAsync("/api/admin/symbols/BTCUSDT", TestContext.Current.CancellationToken);

        Assert.Equal(
            Now.UtcDateTime,
            await TestDatabase.ScalarAsync<DateTime>(connectionString, "SELECT RemovedAt FROM MonitoredSymbols WHERE Symbol = 'BTCUSDT'"));
    }

    [Fact]
    public async Task Remove_SymbolWithInvalidShape_ReturnsBadRequest()
    {
        await using var factory = NewFactory(out _);
        using var client = AdminClient(factory);

        var response = await client.DeleteAsync("/api/admin/symbols/AB", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Collection_SymbolRemovedWhileConnected_StopsStoringItsCandles()
    {
        await using var factory = NewFactory(out var connectionString);
        using var client = AdminClient(factory);
        await factory.Binance.Connected;
        await client.DeleteAsync("/api/admin/symbols/BTCUSDT", TestContext.Current.CancellationToken);

        factory.Binance.PublishClosed(Candle("BTCUSDT", minute: 0, close: 100m));
        factory.Binance.PublishClosed(Candle("ETHUSDT", minute: 0, close: 200m));
        await Poll.UntilAsync(
            async () => await TestDatabase.ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM Candles WHERE Symbol = 'ETHUSDT'") == 1,
            "the candle of a still monitored symbol to be stored");

        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM Candles WHERE Symbol = 'BTCUSDT'"));
    }

    [Fact]
    public async Task Add_NewTradingSymbol_ReturnsCreatedAndListsIt()
    {
        await using var factory = NewFactory(out _);
        using var client = AdminClient(factory);

        var response = await client.PutAsync("/api/admin/symbols/xrpusdt", content: null, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadFromJsonAsync<MonitoredSymbolResponse>(TestContext.Current.CancellationToken);
        var list = await client.GetFromJsonAsync<MonitoredSymbolsResponse>("/api/admin/symbols", TestContext.Current.CancellationToken);
        Assert.Equal(
            (HttpStatusCode.Created, "XRPUSDT", Now.UtcDateTime, true),
            (response.StatusCode, body!.Symbol, body.AddedAt, list!.Symbols.Any(symbol => symbol.Symbol == "XRPUSDT")));
    }

    [Fact]
    public async Task Add_SymbolBinanceDoesNotTrade_ReturnsUnprocessableAndSavesNothing()
    {
        await using var factory = NewFactory(out var connectionString);
        using var client = AdminClient(factory);

        var response = await client.PutAsync("/api/admin/symbols/NOPEUSDT", content: null, TestContext.Current.CancellationToken);

        Assert.Equal(
            (HttpStatusCode.UnprocessableEntity, 0),
            (response.StatusCode, await TestDatabase.ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM MonitoredSymbols WHERE Symbol = 'NOPEUSDT'")));
    }

    [Fact]
    public async Task Add_BinanceUnreachable_ReturnsServiceUnavailableAndSavesNothing()
    {
        await using var factory = NewFactory(out var connectionString);
        using var client = AdminClient(factory);
        factory.Catalog.Unreachable = true;

        var response = await client.PutAsync("/api/admin/symbols/XRPUSDT", content: null, TestContext.Current.CancellationToken);

        Assert.Equal(
            (HttpStatusCode.ServiceUnavailable, 0),
            (response.StatusCode, await TestDatabase.ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM MonitoredSymbols WHERE Symbol = 'XRPUSDT'")));
    }

    [Fact]
    public async Task Add_AlreadyMonitoredSymbol_ReturnsOkChangesNothingAndSkipsBinance()
    {
        await using var factory = NewFactory(out _);
        using var client = AdminClient(factory);
        factory.Clock.Advance(TimeSpan.FromHours(1));

        var response = await client.PutAsync("/api/admin/symbols/BTCUSDT", content: null, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadFromJsonAsync<MonitoredSymbolResponse>(TestContext.Current.CancellationToken);
        Assert.Equal(
            (HttpStatusCode.OK, Now.UtcDateTime, 0),
            (response.StatusCode, body!.AddedAt, factory.Catalog.Calls));
    }

    [Fact]
    public async Task Add_RemovedSymbol_ReturnsCreatedReusesItsRowAndKeepsItsCandles()
    {
        await using var factory = NewFactory(out var connectionString);
        using var client = AdminClient(factory);
        await factory.Services.GetRequiredService<ICandleRepository>()
            .InsertMissingAsync([Candle("BTCUSDT", minute: 0, close: 100m)], TestContext.Current.CancellationToken);
        await client.DeleteAsync("/api/admin/symbols/BTCUSDT", TestContext.Current.CancellationToken);
        factory.Clock.Advance(TimeSpan.FromHours(1));

        var response = await client.PutAsync("/api/admin/symbols/BTCUSDT", content: null, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadFromJsonAsync<MonitoredSymbolResponse>(TestContext.Current.CancellationToken);
        Assert.Equal(
            (HttpStatusCode.Created, Now.UtcDateTime.AddHours(1), 1, 0, 1),
            (response.StatusCode,
                body!.AddedAt,
                await TestDatabase.ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM MonitoredSymbols WHERE Symbol = 'BTCUSDT'"),
                await TestDatabase.ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM MonitoredSymbols WHERE Symbol = 'BTCUSDT' AND RemovedAt IS NOT NULL"),
                await TestDatabase.ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM Candles WHERE Symbol = 'BTCUSDT'")));
    }

    [Fact]
    public async Task Add_NewSymbolAboveTheCap_ReturnsConflictSavesNothingButStillAcceptsAMonitoredOne()
    {
        await using var factory = NewFactory(out var connectionString);
        await using var capped = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Symbols:MaxMonitored"] = "3" })));
        using var client = AdminClient(capped);

        var overCap = await client.PutAsync("/api/admin/symbols/XRPUSDT", content: null, TestContext.Current.CancellationToken);
        var monitored = await client.PutAsync("/api/admin/symbols/BTCUSDT", content: null, TestContext.Current.CancellationToken);

        Assert.Equal(
            (HttpStatusCode.Conflict, HttpStatusCode.OK, 0),
            (overCap.StatusCode, monitored.StatusCode, await TestDatabase.ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM MonitoredSymbols WHERE Symbol = 'XRPUSDT'")));
    }

    [Fact]
    public async Task Add_NewSymbol_BackfillsItsLast24HoursOfClosedCandles()
    {
        await using var factory = NewFactory(out var connectionString);
        using var client = AdminClient(factory);
        factory.Binance.AddHistory(Candle("XRPUSDT", minute: -60, close: 2.5m));
        factory.Binance.AddHistory(Candle("XRPUSDT", minute: -25 * 60, close: 9m));

        await client.PutAsync("/api/admin/symbols/XRPUSDT", content: null, TestContext.Current.CancellationToken);

        await Poll.UntilAsync(
            async () => await TestDatabase.ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM Candles WHERE Symbol = 'XRPUSDT'") == 1,
            "the backfilled candle to be stored");
        Assert.Equal(2.5m, await TestDatabase.ScalarAsync<decimal>(connectionString, "SELECT [Close] FROM Candles WHERE Symbol = 'XRPUSDT'"));
    }

    [Fact]
    public async Task Add_RemovedSymbol_BackfillsTheMinutesItMissed()
    {
        await using var factory = NewFactory(out var connectionString);
        using var client = AdminClient(factory);
        await client.DeleteAsync("/api/admin/symbols/BTCUSDT", TestContext.Current.CancellationToken);
        factory.Binance.AddHistory(Candle("BTCUSDT", minute: -30, close: 100m));
        factory.Binance.AddHistory(Candle("BTCUSDT", minute: -29, close: 101m));

        await client.PutAsync("/api/admin/symbols/BTCUSDT", content: null, TestContext.Current.CancellationToken);

        await Poll.UntilAsync(
            async () => await TestDatabase.ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM Candles WHERE Symbol = 'BTCUSDT'") == 2,
            "the missed candles to be stored");
    }

    [Fact]
    public async Task Add_BinanceHistoryUnreachable_StillAddsTheSymbolWithoutBackfill()
    {
        await using var factory = NewFactory(out var connectionString);
        using var client = AdminClient(factory);
        factory.Binance.HistoryUnreachable = true;

        var response = await client.PutAsync("/api/admin/symbols/XRPUSDT", content: null, TestContext.Current.CancellationToken);

        Assert.Equal(
            (HttpStatusCode.Created, 1),
            (response.StatusCode, await TestDatabase.ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM MonitoredSymbols WHERE Symbol = 'XRPUSDT'")));
    }

    [Fact]
    public async Task Add_NewSymbol_SubscribesOnTheOpenConnectionAndCollectsItsLiveCandles()
    {
        await using var factory = NewFactory(out var connectionString);
        using var client = AdminClient(factory);
        await factory.Binance.Connected;

        await client.PutAsync("/api/admin/symbols/XRPUSDT", content: null, TestContext.Current.CancellationToken);
        await Poll.UntilAsync(() => Task.FromResult(factory.Binance.Calls.SequenceEqual(["SUBSCRIBE XRPUSDT"])), "the subscribe on the open connection");
        factory.Binance.PublishClosed(Candle("XRPUSDT", minute: 0, close: 2.5m));

        await Poll.UntilAsync(
            async () => await TestDatabase.ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM Candles WHERE Symbol = 'XRPUSDT'") == 1,
            "the live candle of the added symbol to be stored");
    }

    [Fact]
    public async Task Remove_Symbol_UnsubscribesOnTheOpenConnection()
    {
        await using var factory = NewFactory(out _);
        using var client = AdminClient(factory);
        await factory.Binance.Connected;

        await client.DeleteAsync("/api/admin/symbols/ETHUSDT", TestContext.Current.CancellationToken);

        await Poll.UntilAsync(() => Task.FromResult(factory.Binance.Calls.SequenceEqual(["UNSUBSCRIBE ETHUSDT"])), "the unsubscribe on the open connection");
    }

    private ApiFactory NewFactory(out string connectionString)
    {
        connectionString = sql.NewDatabaseConnectionString();
        var factory = new ApiFactory(connectionString);
        factory.Clock.SetUtcNow(Now);
        return factory;
    }

    private static HttpClient AdminClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(AdminApiKeyAttribute.HeaderName, ApiFactory.ValidAdminApiKey);
        return client;
    }

    private static Task<int> RemovedCountAsync(string connectionString, string symbol) =>
        TestDatabase.ScalarAsync<int>(connectionString, $"SELECT COUNT(*) FROM MonitoredSymbols WHERE Symbol = '{symbol}' AND RemovedAt IS NOT NULL");

    private static Candle Candle(string symbol, int minute, decimal close) =>
        new(SymbolCode.Parse(symbol), Now.AddMinutes(minute), close, close, close, close, 1m);
}
