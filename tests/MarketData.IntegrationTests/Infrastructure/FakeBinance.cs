using System.Threading.Channels;
using MarketData.Domain.Candles;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Exceptions;
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
    private readonly List<SymbolCode> _registered = [];
    private bool _isConnected;
    private readonly Lock _lock = new();

    private readonly TaskCompletionSource<IReadOnlyCollection<SymbolCode>> _connected =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes with the Symbols subscribed so far once the host connects.</summary>
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
        lock (_lock)
        {
            _registered.AddRange(symbols.Where(symbol => !_registered.Contains(symbol)));
            _isConnected = true;
            _connected.TrySetResult([.. _registered]);
        }

        return Task.CompletedTask;
    }

    public IAsyncEnumerable<Candle> ReadClosedAsync(CancellationToken cancellationToken) =>
        _candles.Reader.ReadAllAsync(cancellationToken);

    public Task SubscribeAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var added = symbols.Where(symbol => !_registered.Contains(symbol)).ToList();
            _registered.AddRange(added);
            Record("SUBSCRIBE", added);
        }

        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var removed = symbols.Where(_registered.Contains).ToList();
            _registered.RemoveAll(removed.Contains);
            Record("UNSUBSCRIBE", removed);
        }

        return Task.CompletedTask;
    }

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

    /// <summary>Like the real stream, a request is only recorded (sent) on an open connection; before that the change is just remembered. The caller holds the lock.</summary>
    private void Record(string method, List<SymbolCode> symbols)
    {
        if (_isConnected && symbols.Count > 0)
        {
            _calls.Add($"{method} {string.Join(',', symbols.Select(symbol => symbol.Value))}");
        }
    }
}
