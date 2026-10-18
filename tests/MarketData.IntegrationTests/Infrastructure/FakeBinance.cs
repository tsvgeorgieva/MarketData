using System.Threading.Channels;
using MarketData.Domain.Candles;
using MarketData.Domain.Exceptions;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;

namespace MarketData.IntegrationTests.Infrastructure;

/// <summary>
/// A Binance the test controls: the stream records who connected and what was subscribed, and delivers the closed Candles the test publishes;
/// the REST history answers with the Candles the test has made available.
/// </summary>
public sealed class FakeBinance : IBinanceStream, IBinanceHistory
{
    private readonly Channel<Candle> _candles = Channel.CreateUnbounded<Candle>();
    private readonly List<Candle> _history = [];
    private readonly List<string> _calls = [];
    private readonly Lock _lock = new();

    private readonly TaskCompletionSource<IReadOnlyCollection<SymbolCode>> _connected =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes with the Symbols the host asked for once it has connected.</summary>
    public Task<IReadOnlyCollection<SymbolCode>> Connected => _connected.Task;

    /// <summary>While set, a history request fails the way an unreachable Binance does.</summary>
    public bool HistoryUnreachable { get; set; }

    /// <summary>Each subscribe or unsubscribe the host made on the stream, like <c>SUBSCRIBE XRPUSDT</c>, in order.</summary>
    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (_lock)
            {
                return [.. _calls];
            }
        }
    }

    public void PublishClosed(Candle candle) => _candles.Writer.TryWrite(candle);

    /// <summary>Makes a Candle available to the REST history, as if Binance had it all along.</summary>
    public void AddHistory(Candle candle)
    {
        lock (_lock)
        {
            _history.Add(candle);
        }
    }

    public Task ConnectAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken)
    {
        _connected.TrySetResult(symbols);
        return Task.CompletedTask;
    }

    public IAsyncEnumerable<Candle> ReadClosedAsync(CancellationToken cancellationToken) =>
        _candles.Reader.ReadAllAsync(cancellationToken);

    public Task SubscribeAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken) =>
        Record("SUBSCRIBE", symbols);

    public Task UnsubscribeAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken) =>
        Record("UNSUBSCRIBE", symbols);

    public Task<IReadOnlyList<Candle>> GetClosedCandlesAsync(SymbolCode symbol, DateTimeOffset from, DateTimeOffset through, CancellationToken cancellationToken)
    {
        if (HistoryUnreachable)
        {
            throw new UpstreamUnavailableException("Binance cannot be reached.");
        }

        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<Candle>>(
                [.. _history.Where(candle => candle.Symbol == symbol && candle.OpenTime >= from && candle.OpenTime <= through).OrderBy(candle => candle.OpenTime)]);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Task Record(string method, IReadOnlyCollection<SymbolCode> symbols)
    {
        lock (_lock)
        {
            _calls.Add($"{method} {string.Join(',', symbols.Select(symbol => symbol.Value))}");
        }

        return Task.CompletedTask;
    }
}
