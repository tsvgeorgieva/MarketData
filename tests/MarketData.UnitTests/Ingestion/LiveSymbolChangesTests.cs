using MarketData.Application.Ingestion;
using MarketData.Domain.Symbols;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MarketData.UnitTests.Ingestion;

/// <summary>Runs the feed job against an open fake Binance connection and changes the Monitored Symbols while it runs.</summary>
public sealed class LiveSymbolChangesTests : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly OpenBinanceFeed _feed = new();
    private readonly SymbolChanges _changes = new();
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
    public async Task SymbolChange_ForASymbolAlreadyInThatState_DoesNothingOnTheConnection()
    {
        Start(new FakeMonitoredSymbols("BTCUSDT"));
        await _feed.Connected;

        _changes.Publish(new SymbolChange(SymbolCode.Parse("BTCUSDT"), Added: true));
        _changes.Publish(new SymbolChange(SymbolCode.Parse("XRPUSDT"), Added: false));
        _changes.Publish(new SymbolChange(SymbolCode.Parse("ETHUSDT"), Added: true));

        // The last change is the only one that reaches Binance, which also shows the first two were handled before it.
        await UntilAsync(() => _feed.Calls.SequenceEqual(["SUBSCRIBE ETHUSDT"]));
    }

    [Fact]
    public async Task NoMonitoredSymbols_FirstSymbolAdded_ConnectsWithIt()
    {
        var symbols = new FakeMonitoredSymbols();
        Start(symbols);

        symbols.Add("XRPUSDT");
        _changes.Publish(new SymbolChange(SymbolCode.Parse("XRPUSDT"), Added: true));

        var connectedWith = await _feed.Connected.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        Assert.Equal(["XRPUSDT"], connectedWith.Select(symbol => symbol.Value));
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
        var service = new IngestionService(symbols, _feed, _queue, _changes, NullLogger<IngestionService>.Instance);
        _running = service.RunAsync(_stop.Token);
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
