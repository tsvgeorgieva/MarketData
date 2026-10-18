using MarketData.Domain.Prices;

namespace MarketData.Application.Prices.Interfaces;

public interface IPriceAnalyticsService
{
    /// <summary>The mean Price of the Candles opened in the last 24 hours, using only the Candles that exist.</summary>
    Task<AveragePrice> GetAveragePrice24hAsync(string symbolText, CancellationToken cancellationToken);


    /// <summary>
    /// The mean of the last <c>N</c> Intervals of the request's Period, ending with the one that contains the last minute closed by its end
    /// (now when omitted or in the future).
    /// </summary>
    Task<SimpleMovingAverage> GetSimpleMovingAverageAsync(SimpleMovingAverageRequest request, CancellationToken cancellationToken);
}
