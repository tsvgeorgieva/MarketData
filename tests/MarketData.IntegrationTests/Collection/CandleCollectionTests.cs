using System.Globalization;
using MarketData.Application.Ingestion;
using MarketData.Domain.Candles;
using MarketData.Domain.Symbols;
using MarketData.IntegrationTests.Infrastructure;

namespace MarketData.IntegrationTests.Collection;

public class CandleCollectionTests(SqlServerFixture sql)
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ClosedUpdate_Published_IsStoredWithFullPrecision()
    {
        var connectionString = sql.NewDatabaseConnectionString();
        await using var factory = new ApiFactory(connectionString);
        using var client = factory.CreateClient();
        await factory.Binance.Connected;

        factory.Binance.Publish(Closed("BTCUSDT", minute: 0, close: 81589.12345678m));

        await Poll.UntilAsync(async () => await CloseOf(connectionString, "BTCUSDT", 0) == 81589.12345678m, "the closed candle to be stored");
    }

    [Fact]
    public async Task UnclosedUpdate_Published_IsNotStored()
    {
        var connectionString = sql.NewDatabaseConnectionString();
        await using var factory = new ApiFactory(connectionString);
        using var client = factory.CreateClient();
        await factory.Binance.Connected;

        factory.Binance.Publish(Unclosed("BTCUSDT", minute: 0, close: 100m));
        factory.Binance.Publish(Closed("BTCUSDT", minute: 1, close: 101m));
        await Poll.UntilAsync(async () => await CountAt(connectionString, "BTCUSDT", 1) == 1, "the later closed candle to be stored");

        Assert.Equal(0, await CountAt(connectionString, "BTCUSDT", 0));
    }

    [Fact]
    public async Task DuplicateCandle_PublishedAgain_KeepsTheFirstStoredValues()
    {
        var connectionString = sql.NewDatabaseConnectionString();
        await using var factory = new ApiFactory(connectionString);
        using var client = factory.CreateClient();
        await factory.Binance.Connected;

        factory.Binance.Publish(Closed("BTCUSDT", minute: 0, close: 100m));
        factory.Binance.Publish(Closed("BTCUSDT", minute: 0, close: 200m));
        factory.Binance.Publish(Closed("BTCUSDT", minute: 1, close: 101m));
        await Poll.UntilAsync(async () => await CountAt(connectionString, "BTCUSDT", 1) == 1, "the later candle to be stored");

        Assert.Equal(100m, await CloseOf(connectionString, "BTCUSDT", 0));
    }

    [Fact]
    public async Task FirstRun_SeedsTheConfiguredSymbolsAndCollectsThem()
    {
        await using var factory = new ApiFactory(sql.NewDatabaseConnectionString());
        using var client = factory.CreateClient();

        var symbols = await factory.Binance.Connected;

        Assert.Equal(["ADAUSDT", "BTCUSDT", "ETHUSDT"], symbols.Select(symbol => symbol.Value));
    }

    [Fact]
    public async Task Restart_AfterASymbolWasRemoved_DoesNotSeedItAgain()
    {
        var connectionString = sql.NewDatabaseConnectionString();
        var first = new ApiFactory(connectionString);
        using (first.CreateClient())
        {
            await first.Binance.Connected;
        }

        await TestDatabase.ExecuteAsync(connectionString, "UPDATE MonitoredSymbols SET RemovedAt = SYSUTCDATETIME() WHERE Symbol = 'ETHUSDT'");
        await first.DisposeAsync();

        await using var second = new ApiFactory(connectionString);
        using var client = second.CreateClient();
        var symbols = await second.Binance.Connected;

        Assert.Equal(["ADAUSDT", "BTCUSDT"], symbols.Select(symbol => symbol.Value));
    }

    private static KlineUpdate Closed(string symbol, int minute, decimal close) => new(Candle(symbol, minute, close), IsClosed: true);

    private static KlineUpdate Unclosed(string symbol, int minute, decimal close) => new(Candle(symbol, minute, close), IsClosed: false);

    private static Candle Candle(string symbol, int minute, decimal close) =>
        new(SymbolCode.Parse(symbol), Start.AddMinutes(minute), close, close, close, close, 1m);

    private static Task<int> CountAt(string connectionString, string symbol, int minute) =>
        TestDatabase.ScalarAsync<int>(connectionString, $"SELECT COUNT(*) FROM Candles WHERE Symbol = '{symbol}' AND OpenTime = '{OpenTimeText(minute)}'");

    private static async Task<decimal?> CloseOf(string connectionString, string symbol, int minute)
    {
        var close = await TestDatabase.ScalarAsync<object?>(
            connectionString,
            $"SELECT [Close] FROM Candles WHERE Symbol = '{symbol}' AND OpenTime = '{OpenTimeText(minute)}'");
        return close as decimal?;
    }

    private static string OpenTimeText(int minute) =>
        Start.AddMinutes(minute).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
