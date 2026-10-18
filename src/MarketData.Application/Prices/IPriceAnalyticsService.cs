using MarketData.Domain.Prices;

namespace MarketData.Application.Prices;

public interface IPriceAnalyticsService
{
    /// <summary>The mean Price of the Candles opened in the last 24 hours, using only the Candles that exist.</summary>
    Task<AveragePrice> GetAveragePrice24hAsync(string symbolText, CancellationToken cancellationToken);
}
