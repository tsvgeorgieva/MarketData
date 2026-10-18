using MarketData.Domain.Candles;
using MarketData.Domain.Symbols;

namespace MarketData.Domain.Interfaces;

public interface IBinanceStream : IAsyncDisposable
{
    /// <summary>Yields each Candle once it has closed, until Binance ends the stream or the token is cancelled.</summary>
    IAsyncEnumerable<Candle> ReadClosedAsync(CancellationToken cancellationToken);

    /// <summary>Adds the Symbols to the open connection, without reconnecting.</summary>
    Task SubscribeAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken);

    /// <summary>Removes the Symbols from the open connection, without reconnecting.</summary>
    Task UnsubscribeAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken);
}
