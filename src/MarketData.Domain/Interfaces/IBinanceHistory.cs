using MarketData.Domain.Candles;
using MarketData.Domain.Symbols;

namespace MarketData.Domain.Interfaces;

public interface IBinanceHistory
{
    /// <summary>
    /// The closed one-minute Candles of the Symbol that opened from <paramref name="from"/> to <paramref name="through"/> (both included), oldest first.
    /// Throws <see cref="Exceptions.UpstreamUnavailableException"/> when Binance cannot be reached.
    /// </summary>
    Task<IReadOnlyList<Candle>> GetClosedCandlesAsync(SymbolCode symbol, DateTimeOffset from, DateTimeOffset through, CancellationToken cancellationToken);
}
