using MarketData.Application.Ingestion;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MarketData.UnitTests.Ingestion;

public class IngestionServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly FakeCandleStore _store = new();

    [Fact]
    public async Task RunAsync_ClosedAndOpenUpdates_StoresOnlyTheClosedCandles()
    {
        var feed = new ScriptedBinanceFeed([TestCandles.Open(0), TestCandles.Closed(0), TestCandles.Open(1)]);

        await RunAsync(feed, new FakeMonitoredSymbols("BTCUSDT"));

        Assert.Equal([TestCandles.At(0)], _store.Stored);
    }

    [Fact]
    public async Task RunAsync_MonitoredSymbols_ConnectsOnceForAllOfThem()
    {
        var feed = new ScriptedBinanceFeed([]);

        await RunAsync(feed, new FakeMonitoredSymbols("BTCUSDT", "ETHUSDT"));

        Assert.Equal("1 connect: BTCUSDT,ETHUSDT", $"{feed.Connects} connect: {string.Join(',', feed.ConnectedWith!.Select(symbol => symbol.Value))}");
    }

    [Fact]
    public async Task RunAsync_NoMonitoredSymbols_DoesNotConnect()
    {
        var feed = new ScriptedBinanceFeed([]);

        await RunAsync(feed, new FakeMonitoredSymbols());

        Assert.Equal(0, feed.Connects);
    }

    [Fact]
    public async Task RunAsync_DatabaseHolding_FeedIsStillReadToTheEnd()
    {
        var release = new TaskCompletionSource();
        _store.HoldUntil = release.Task;
        var feed = new ScriptedBinanceFeed(Enumerable.Range(0, 50).Select(minute => TestCandles.Closed(minute)));
        var running = RunAsync(feed, new FakeMonitoredSymbols("BTCUSDT"), queueCapacity: 5);

        await feed.Exhausted.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        release.SetResult();
        await running;
        Assert.Contains(TestCandles.At(49), _store.Stored);
    }

    [Fact]
    public async Task RunAsync_DatabaseFailsOnce_RetriesAndStoresTheCandle()
    {
        _store.FailuresToThrow = 1;
        var feed = new ScriptedBinanceFeed([TestCandles.Closed(0)]);

        await RunAsync(feed, new FakeMonitoredSymbols("BTCUSDT"));

        Assert.Equal([TestCandles.At(0)], _store.Stored);
    }

    [Fact]
    public async Task RunAsync_StreamFailsAfterUpdates_StoresWhatArrivedWithoutThrowing()
    {
        var feed = new ScriptedBinanceFeed([TestCandles.Closed(0)], thenFailWith: new IOException("connection lost"));

        await RunAsync(feed, new FakeMonitoredSymbols("BTCUSDT"));

        Assert.Equal([TestCandles.At(0)], _store.Stored);
    }

    private async Task RunAsync(ScriptedBinanceFeed feed, FakeMonitoredSymbols symbols, int queueCapacity = 100)
    {
        var options = Options.Create(new IngestionOptions
        {
            QueueCapacity = queueCapacity,
            MaxBatchSize = 10,
            WriterRetryDelay = TimeSpan.FromMilliseconds(1),
        });
        var writer = new CandleWriter(_store, TimeProvider.System, options, NullLogger<CandleWriter>.Instance);
        var service = new IngestionService(symbols, feed, writer, options, NullLogger<IngestionService>.Instance);

        await service.RunAsync(TestContext.Current.CancellationToken).WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }
}
