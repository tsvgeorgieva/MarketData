using MarketData.Domain.Candles;
using MarketData.Domain.Symbols;

namespace MarketData.Domain.Interfaces;

/// <summary>The one Binance stream of one-minute Candles for all Monitored Symbols.</summary>
public interface IBinanceStream : IAsyncDisposable
{
    /// <summary>Opens the connection for the given Symbols, plus any added through <see cref="SubscribeAsync"/> before it opened.</summary>
    Task ConnectAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken);

    /// <summary>Yields each Candle once it has closed, until Binance ends the stream or the token is cancelled.</summary>
    IAsyncEnumerable<Candle> ReadClosedAsync(CancellationToken cancellationToken);

    /// <summary>Adds the Symbols to the stream. Before the connection is open the change applies when it opens; one already subscribed is ignored.</summary>
    Task SubscribeAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken);

    /// <summary>Removes the Symbols from the stream. Before the connection is open the change applies when it opens; one not subscribed is ignored.</summary>
    Task UnsubscribeAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken);
}
