using MarketData.Domain.Symbols;

namespace MarketData.Application.Ingestion;

public interface IBinanceStreamFactory
{
    /// <summary>Opens one stream of one-minute updates for all the given Symbols.</summary>
    Task<IBinanceStream> ConnectAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken);
}
