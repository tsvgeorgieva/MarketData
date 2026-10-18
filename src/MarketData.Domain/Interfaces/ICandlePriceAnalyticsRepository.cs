using MarketData.Domain.Candles;
using MarketData.Domain.Prices;
using MarketData.Domain.Symbols;

namespace MarketData.Domain.Interfaces;

public interface ICandlePriceAnalyticsRepository
{
    /// <summary>
    /// The mean Price of the last Candle of each <paramref name="period"/> Interval among the Candles opened from <paramref name="from"/> to <paramref name="through"/>
    /// (both included), with the open time of the first of those Candles and the close of the last. The database cuts the mean off at eight decimal places. Null when there are none.
    /// </summary>
    Task<AveragePrice?> GetAveragePriceOfLastCandlesPerPeriodAsync(SymbolCode symbol, Period period, DateTime from, DateTime through, CancellationToken cancellationToken);
}
