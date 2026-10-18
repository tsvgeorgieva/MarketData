using System.Threading.Channels;
using MarketData.Application.Ingestion;
using MarketData.Domain.Symbols;

namespace MarketData.IntegrationTests.Infrastructure;

/// <summary>A Binance feed the test controls: it records who connected and delivers whatever the test publishes.</summary>
public sealed class FakeBinance : IBinanceStreamFactory
{
    private readonly Channel<KlineUpdate> _updates = Channel.CreateUnbounded<KlineUpdate>();

    private readonly TaskCompletionSource<IReadOnlyCollection<SymbolCode>> _connected =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes with the Symbols the host asked for once it has connected.</summary>
    public Task<IReadOnlyCollection<SymbolCode>> Connected => _connected.Task;

    public void Publish(KlineUpdate update) => _updates.Writer.TryWrite(update);

    public Task<IBinanceStream> ConnectAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken)
    {
        _connected.TrySetResult(symbols);
        return Task.FromResult<IBinanceStream>(new Stream(_updates.Reader));
    }

    private sealed class Stream(ChannelReader<KlineUpdate> updates) : IBinanceStream
    {
        public IAsyncEnumerable<KlineUpdate> ReadAsync(CancellationToken cancellationToken) =>
            updates.ReadAllAsync(cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
