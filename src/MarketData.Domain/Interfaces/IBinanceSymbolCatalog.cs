using MarketData.Domain.Symbols;

namespace MarketData.Domain.Interfaces;

public interface IBinanceSymbolCatalog
{
    /// <summary>Whether Binance lists the Symbol and it is currently trading; throws when Binance cannot be reached.</summary>
    Task<bool> IsTradingAsync(SymbolCode symbol, CancellationToken cancellationToken);
}
