using System.Threading.Channels;
using MarketData.Domain.Candles;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;

namespace MarketData.UnitTests.Ingestion;

/// <summary>A Binance connection that stays open until the test ends it: delivers the Candles the test publishes and records every subscribe and unsubscribe.</summary>
internal sealed class OpenBinanceFeed : IBinanceStream
{
    private readonly Channel<Candle> _candles = Channel.CreateUnbounded<Candle>();
    private readonly List<string> _calls = [];
    private readonly Lock _lock = new();

    private readonly TaskCompletionSource<IReadOnlyCollection<SymbolCode>> _connected =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes with the Symbols the service connected with.</summary>
    public Task<IReadOnlyCollection<SymbolCode>> Connected => _connected.Task;

    /// <summary>Each call made on the open connection, like <c>SUBSCRIBE XRPUSDT</c>, in order.</summary>
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

    public Task ConnectAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken)
    {
        _connected.TrySetResult(symbols);
        return Task.CompletedTask;
    }

    public IAsyncEnumerable<Candle> ReadClosedAsync(CancellationToken cancellationToken) => _candles.Reader.ReadAllAsync(cancellationToken);

    public Task SubscribeAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken) => Record("SUBSCRIBE", symbols);

    public Task UnsubscribeAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken) => Record("UNSUBSCRIBE", symbols);

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
