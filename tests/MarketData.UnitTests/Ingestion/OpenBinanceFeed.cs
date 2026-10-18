using System.Threading.Channels;
using MarketData.Domain.Candles;
using MarketData.Domain.Symbols;
using MarketData.Domain.Interfaces;

namespace MarketData.UnitTests.Ingestion;

/// <summary>
/// A Binance stream that stays open until the test ends it. Like the real one it remembers Symbols subscribed before it connects,
/// sends subscribe and unsubscribe requests only once connected (recorded in <see cref="Calls"/>), and delivers the Candles the test publishes.
/// </summary>
internal sealed class OpenBinanceFeed : IBinanceStream
{
    private readonly Channel<Candle> _candles = Channel.CreateUnbounded<Candle>();
    private readonly List<string> _calls = [];
    private readonly HashSet<SymbolCode> _registered = [];
    private readonly Lock _lock = new();
    private readonly TaskCompletionSource _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _isConnected;

    /// <summary>While set, connecting waits until the task completes.</summary>
    public Task? HoldConnectUntil { get; set; }

    /// <summary>Completes when the service has connected.</summary>
    public Task Connected => _connected.Task;

    /// <summary>The Symbols subscribed so far, whether before or after connecting.</summary>
    public IReadOnlyCollection<string> Registered
    {
        get
        {
            lock (_lock)
            {
                return [.. _registered.Select(symbol => symbol.Value)];
            }
        }
    }

    /// <summary>Each request sent on the open connection, like <c>SUBSCRIBE XRPUSDT</c>, in order.</summary>
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

    public void Publish(Candle candle) => _candles.Writer.TryWrite(candle);

    public async Task ConnectAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken)
    {
        if (HoldConnectUntil is not null)
        {
            await HoldConnectUntil.WaitAsync(cancellationToken);
        }

        lock (_lock)
        {
            _registered.UnionWith(symbols);
            _isConnected = true;
        }

        _connected.TrySetResult();
    }

    public IAsyncEnumerable<Candle> ReadClosedAsync(CancellationToken cancellationToken) => _candles.Reader.ReadAllAsync(cancellationToken);

    public Task SubscribeAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var added = symbols.Where(_registered.Add).ToList();
            if (_isConnected && added.Count > 0)
            {
                _calls.Add($"SUBSCRIBE {string.Join(',', added.Select(symbol => symbol.Value))}");
            }
        }

        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var removed = symbols.Where(_registered.Remove).ToList();
            if (_isConnected && removed.Count > 0)
            {
                _calls.Add($"UNSUBSCRIBE {string.Join(',', removed.Select(symbol => symbol.Value))}");
            }
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
