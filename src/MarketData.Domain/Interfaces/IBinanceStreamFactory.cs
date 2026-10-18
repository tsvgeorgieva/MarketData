using MarketData.Domain.Symbols;

namespace MarketData.Domain.Interfaces;

public interface IBinanceStreamFactory
{
    /// <summary>Opens one stream of one-minute Candles for all the given Symbols.</summary>
    Task<IBinanceStream> ConnectAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken);
}
