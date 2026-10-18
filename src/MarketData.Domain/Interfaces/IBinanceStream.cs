using MarketData.Domain.Candles;
using MarketData.Domain.Symbols;

namespace MarketData.Domain.Interfaces;

/// <summary>The one Binance stream of one-minute Candles for all Monitored Symbols.</summary>
public interface IBinanceStream : IAsyncDisposable
{
    /// <summary>Opens the connection for the given Symbols plus any subscribed since. A connection needs at least one, so with none this waits until one is subscribed.</summary>
    Task ConnectAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken);

    /// <summary>Yields each Candle once it has closed, until Binance ends the stream or the token is cancelled. Needs an open connection.</summary>
    IAsyncEnumerable<Candle> ReadClosedAsync(CancellationToken cancellationToken);

    /// <summary>Adds the Symbols to the stream: sent to Binance on an open connection, otherwise remembered for when it opens. One already subscribed is ignored.</summary>
    Task SubscribeAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken);

    /// <summary>Removes the Symbols from the stream: sent to Binance on an open connection, otherwise forgotten. One not subscribed is ignored.</summary>
    Task UnsubscribeAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken);
}
