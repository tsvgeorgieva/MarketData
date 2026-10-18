using MarketData.Application.Ingestion;
using MarketData.Application.Symbols;
using MarketData.Domain.Symbols;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MarketData.UnitTests.Ingestion;

/// <summary>Runs the feed job against an open fake Binance connection and changes the Monitored Symbols while it runs.</summary>
public sealed class LiveSymbolChangesTests : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly OpenBinanceFeed _feed = new();
    private readonly SymbolChangesQueue _changes = new(Options.Create(new IngestionOptions()), NullLogger<SymbolChangesQueue>.Instance);
    private readonly CandleQueue _queue = new(Options.Create(new IngestionOptions { QueueCapacity = 100 }), NullLogger<CandleQueue>.Instance);
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private Task _running = Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await _running.WaitAsync(Timeout);
        _stop.Dispose();
    }

    [Fact]
    public async Task SymbolAdded_WhileConnected_SubscribesOnTheOpenConnection()
    {
        Start(new FakeMonitoredSymbols("BTCUSDT"));
        await _feed.Connected;

        _changes.Publish(new SymbolChange(SymbolCode.Parse("XRPUSDT"), Added: true));

        await UntilAsync(() => _feed.Calls.SequenceEqual(["SUBSCRIBE XRPUSDT"]));
    }

    [Fact]
    public async Task SymbolRemoved_WhileConnected_UnsubscribesOnTheOpenConnection()
    {
        Start(new FakeMonitoredSymbols("BTCUSDT", "ETHUSDT"));
        await _feed.Connected;

        _changes.Publish(new SymbolChange(SymbolCode.Parse("ETHUSDT"), Added: false));

        await UntilAsync(() => _feed.Calls.SequenceEqual(["UNSUBSCRIBE ETHUSDT"]));
    }

    [Fact]
    public async Task Candles_OfAnAddedAndARemovedSymbol_OnlyTheAddedOnesAreQueued()
    {
        var symbols = new FakeMonitoredSymbols("BTCUSDT", "ETHUSDT");
        Start(symbols);
        await _feed.Connected;
        symbols.Add("XRPUSDT");
        symbols.Remove("ETHUSDT");

        _feed.Publish(TestCandles.At(0, "ETHUSDT"));
        _feed.Publish(TestCandles.At(0, "XRPUSDT"));

        // The removed symbol's candle was published first, so once the added symbol's candle is queued the other one has been dealt with.
        var queued = new List<string>();
        await UntilAsync(() =>
        {
            while (_queue.TryRead(out var candle))
            {
                queued.Add(candle.Symbol.Value);
            }

            return queued.Contains("XRPUSDT");
        });
        Assert.Equal(["XRPUSDT"], queued);
    }

    private void Start(FakeMonitoredSymbols symbols)
    {
        var ingestion = new IngestionService(symbols, _feed, _queue, NullLogger<IngestionService>.Instance);
        var handler = new SymbolChangeEventHandler(_changes, _feed, NullLogger<SymbolChangeEventHandler>.Instance);
        _running = Task.WhenAll(ingestion.RunAsync(_stop.Token), handler.RunAsync(_stop.Token));
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
