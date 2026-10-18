using MarketData.Application.Ingestion;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MarketData.UnitTests.Ingestion;

/// <summary>Runs the feed job and the writer job together, as the host does, against hand-written fakes.</summary>
public sealed class IngestionServiceTests : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly FakeCandleRepository _repository = new();
    private readonly CancellationTokenSource _writerStop = new();
    private Task _writing = Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await StopWriterAsync();
        _writerStop.Dispose();
    }

    [Fact]
    public async Task RunAsync_ClosedCandles_AreStored()
    {
        var feed = new ScriptedBinanceFeed([TestCandles.At(0), TestCandles.At(1)]);

        await RunAsync(feed, new FakeMonitoredSymbols("BTCUSDT"));
        await StopWriterAsync();

        Assert.Equal([TestCandles.At(0), TestCandles.At(1)], _repository.Stored);
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
        _repository.HoldUntil = release.Task;
        var feed = new ScriptedBinanceFeed(Enumerable.Range(0, 50).Select(minute => TestCandles.At(minute)));

        await RunAsync(feed, new FakeMonitoredSymbols("BTCUSDT"), queueCapacity: 5);

        release.SetResult();
        await StopWriterAsync();
        Assert.Contains(TestCandles.At(49), _repository.Stored);
    }

    [Fact]
    public async Task RunAsync_DatabaseFailsOnce_WriterRetriesAndStoresTheCandle()
    {
        _repository.FailuresToThrow = 1;
        var feed = new ScriptedBinanceFeed([TestCandles.At(0)]);

        await RunAsync(feed, new FakeMonitoredSymbols("BTCUSDT"));
        await UntilStoredAsync(1);

        Assert.Equal([TestCandles.At(0)], _repository.Stored);
    }

    [Fact]
    public async Task RunAsync_StreamFailsAfterCandles_StoresWhatArrivedWithoutThrowing()
    {
        var feed = new ScriptedBinanceFeed([TestCandles.At(0)], thenFailWith: new IOException("connection lost"));

        await RunAsync(feed, new FakeMonitoredSymbols("BTCUSDT"));
        await StopWriterAsync();

        Assert.Equal([TestCandles.At(0)], _repository.Stored);
    }

    private async Task RunAsync(ScriptedBinanceFeed feed, FakeMonitoredSymbols symbols, int queueCapacity = 100)
    {
        var options = Options.Create(new IngestionOptions
        {
            QueueCapacity = queueCapacity,
            MaxBatchSize = 10,
            WriterRetryDelay = TimeSpan.FromMilliseconds(1),
        });
        var queue = new CandleQueue(options, NullLogger<CandleQueue>.Instance);
        var writer = new CandleWriterService(queue, _repository, TimeProvider.System, options, NullLogger<CandleWriterService>.Instance);
        var service = new IngestionService(symbols, feed, queue, NullLogger<IngestionService>.Instance);

        _writing = writer.RunAsync(_writerStop.Token);
        await service.RunAsync(TestContext.Current.CancellationToken).WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    private async Task StopWriterAsync()
    {
        await _writerStop.CancelAsync();
        await _writing.WaitAsync(Timeout);
    }

    private async Task UntilStoredAsync(int count)
    {
        while (_repository.Stored.Count < count)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken).WaitAsync(Timeout, TestContext.Current.CancellationToken);
        }
    }
}
