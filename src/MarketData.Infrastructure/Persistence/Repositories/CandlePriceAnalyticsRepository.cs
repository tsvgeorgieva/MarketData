using System.Data;
using MarketData.Domain.Candles;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Prices;
using MarketData.Domain.Symbols;
using Microsoft.Data.SqlClient;
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

    public async Task<IReadOnlyList<Candle>> GetLastCandlesPerPeriodAsync(
        SymbolCode symbol, Period period, DateTime from, DateTime through, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // The Candle that opened last in each Bucket, found by numbering a Period's Candles from the newest. Periods are counted in minutes
        // from 1970-01-05, a Monday midnight, so weeks start on Monday. The symbol is passed as varchar like its column to keep the key seek.
        return await db.Candles
            .FromSqlRaw(
                """
                SELECT Symbol, OpenTime, [Open], High, Low, [Close], Volume
                FROM (
                    SELECT *,
                        ROW_NUMBER() OVER (
                            PARTITION BY DATEDIFF_BIG(minute, '1970-01-05', OpenTime) / @periodMinutes
                            ORDER BY OpenTime DESC) AS Newest
                    FROM Candles
                    WHERE Symbol = @symbol AND OpenTime >= @from AND OpenTime <= @through
                ) AS numbered
                WHERE Newest = 1
                """,
                new SqlParameter("periodMinutes", (long)period.Length.TotalMinutes),
                new SqlParameter("symbol", SqlDbType.VarChar, 20) { Value = symbol.Value },
                new SqlParameter("from", SqlDbType.DateTime2) { Value = from },
                new SqlParameter("through", SqlDbType.DateTime2) { Value = through })
            .AsNoTracking()
            .OrderBy(candle => candle.OpenTime)
            .ToListAsync(cancellationToken);
    }
}
