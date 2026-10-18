using System.Threading.Channels;
using MarketData.Domain.Candles;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;

namespace MarketData.IntegrationTests.Infrastructure;

/// <summary>A Binance feed the test controls: it records who connected and delivers the closed Candles the test publishes.</summary>
public sealed class FakeBinance : IBinanceStreamFactory
{
    private readonly Channel<Candle> _candles = Channel.CreateUnbounded<Candle>();

    private readonly TaskCompletionSource<IReadOnlyCollection<SymbolCode>> _connected =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes with the Symbols the host asked for once it has connected.</summary>
    public Task<IReadOnlyCollection<SymbolCode>> Connected => _connected.Task;

    public void PublishClosed(Candle candle) => _candles.Writer.TryWrite(candle);

    public Task<IBinanceStream> ConnectAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken)
    {
        _connected.TrySetResult(symbols);
        return Task.FromResult<IBinanceStream>(new Stream(_candles.Reader));
    }

    private sealed class Stream(ChannelReader<Candle> candles) : IBinanceStream
    {
        public IAsyncEnumerable<Candle> ReadClosedAsync(CancellationToken cancellationToken) =>
            candles.ReadAllAsync(cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
