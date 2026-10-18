using System.Runtime.CompilerServices;
using MarketData.Domain.Candles;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;

namespace MarketData.UnitTests.Ingestion;

/// <summary>Plays a fixed list of closed Candles, then ends or fails, and records how it was used.</summary>
internal sealed class ScriptedBinanceFeed(IEnumerable<Candle> candles, Exception? thenFailWith = null)
    : IBinanceStream
{
    public int Connects { get; private set; }

    public IReadOnlyCollection<SymbolCode>? ConnectedWith { get; private set; }

    public Task ConnectAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken)
    {
        Connects++;
        ConnectedWith = symbols;
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<Candle> ReadClosedAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        foreach (var candle in candles)
        {
            yield return candle;
        }

        if (thenFailWith is not null)
        {
            throw thenFailWith;
        }
    }

    public Task SubscribeAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task UnsubscribeAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
