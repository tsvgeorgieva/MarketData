using System.Threading.Channels;
using MarketData.Domain.Candles;
using MarketData.Domain.Exceptions;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;

namespace MarketData.IntegrationTests.Infrastructure;

/// <summary>
/// A Binance the test controls: the feed records who connected and delivers the closed Candles the test publishes,
/// and the REST history answers with the Candles the test has made available.
/// </summary>
public sealed class FakeBinance : IBinanceStreamFactory, IBinanceHistory
{
    private readonly Channel<Candle> _candles = Channel.CreateUnbounded<Candle>();
    private readonly List<Candle> _history = [];
    private readonly Lock _lock = new();

    private readonly TaskCompletionSource<IReadOnlyCollection<SymbolCode>> _connected =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes with the Symbols the host asked for once it has connected.</summary>
    public Task<IReadOnlyCollection<SymbolCode>> Connected => _connected.Task;

    /// <summary>While set, a history request fails the way an unreachable Binance does.</summary>
    public bool HistoryUnreachable { get; set; }

    public void PublishClosed(Candle candle) => _candles.Writer.TryWrite(candle);

    /// <summary>Makes a Candle available to the REST history, as if Binance had it all along.</summary>
    public void AddHistory(Candle candle)
    {
        lock (_lock)
        {
            _history.Add(candle);
        }
    }

    public Task<IBinanceStream> ConnectAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken)
    {
        _connected.TrySetResult(symbols);
        return Task.FromResult<IBinanceStream>(new Stream(_candles.Reader));
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

    private sealed class Stream(ChannelReader<Candle> candles) : IBinanceStream
    {
        public IAsyncEnumerable<Candle> ReadClosedAsync(CancellationToken cancellationToken) =>
            candles.ReadAllAsync(cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
