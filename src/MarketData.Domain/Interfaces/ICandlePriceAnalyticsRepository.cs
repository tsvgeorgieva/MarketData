using MarketData.Domain.Prices;
using MarketData.Domain.Symbols;

namespace MarketData.Domain.Interfaces;

public interface ICandlePriceAnalyticsRepository
{
    /// <summary>
    /// The mean Price of the Symbol's Candles opened from <paramref name="windowStart"/> up to but not including <paramref name="windowEnd"/>,
    /// using only the Candles that exist. The database computes the mean and cuts it off at eight decimal places. Null when there are none.
    /// </summary>
    Task<AveragePrice?> GetAveragePriceAsync(SymbolCode symbol, DateTimeOffset windowStart, DateTimeOffset windowEnd, CancellationToken cancellationToken);
}
