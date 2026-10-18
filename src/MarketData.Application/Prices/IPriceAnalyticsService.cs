using MarketData.Domain.Prices;

namespace MarketData.Application.Prices;

public interface IPriceAnalyticsService
{
    /// <summary>The mean Price of the Candles opened in the last 24 hours, using only the Candles that exist.</summary>
    Task<AveragePrice> GetAveragePrice24hAsync(string symbolText, CancellationToken cancellationToken);

    /// <summary>
    /// The mean of the last <paramref name="n"/> Buckets of length <paramref name="p"/> ending with the one that contains the last minute closed by
    /// <paramref name="e"/> (a UTC time without an offset; now when omitted or in the future).
    /// </summary>
    Task<SimpleMovingAverage> GetSimpleMovingAverageAsync(string symbolText, int n, string p, DateTime? e, CancellationToken cancellationToken);
}
