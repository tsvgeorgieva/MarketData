using MarketData.Domain.Candles;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Prices;
using MarketData.Domain.Symbols;
using Microsoft.EntityFrameworkCore;

namespace MarketData.Infrastructure.Persistence.Repositories;

internal sealed class CandlePriceAnalyticsRepository(IDbContextFactory<MarketDataDbContext> dbFactory) : ICandlePriceAnalyticsRepository
{
    public async Task<AveragePrice?> GetAveragePriceAsync(SymbolCode symbol, DateTimeOffset windowStart, DateTimeOffset windowEnd, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // The database returns four numbers however many Candles match; the primary key makes this a range seek.
        // SQL Server's AVG of decimal(18,8) is decimal(38,8): the mean is cut off at eight places, not rounded.
        var result = await db.Candles
            .AsNoTracking()
            .Where(candle => candle.Symbol == symbol && candle.OpenTime >= windowStart && candle.OpenTime < windowEnd)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Average = group.Average(candle => candle.Close),
                Count = group.Count(),
                FirstOpenTime = group.Min(candle => candle.OpenTime),
                LastOpenTime = group.Max(candle => candle.OpenTime),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return result is null
            ? null
            : new AveragePrice(
                symbol,
                result.FirstOpenTime,
                result.LastOpenTime + Candle.Length,
                result.Average,
                result.Count);
    }
}
