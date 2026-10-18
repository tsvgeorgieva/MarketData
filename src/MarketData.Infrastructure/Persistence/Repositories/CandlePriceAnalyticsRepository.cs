using System.Data;
using MarketData.Domain.Candles;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Prices;
using MarketData.Domain.Symbols;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MarketData.Infrastructure.Persistence.Repositories;

// Both queries return one row of four numbers however many Candles match, or no row when none do (HAVING).
// SQL Server's AVG of decimal(18,8) is decimal(38,8): the mean is cut off at eight places, not rounded.
// The symbol is passed as varchar like its column, so the primary key can be used for a range seek.
internal sealed class CandlePriceAnalyticsRepository(IDbContextFactory<MarketDataDbContext> dbFactory) : ICandlePriceAnalyticsRepository
{
    public async Task<AveragePrice?> GetAveragePriceOfLastCandlesPerPeriodAsync(
        SymbolCode symbol, Period period, DateTime from, DateTime through, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // The Candle that opened last in each Interval, found by numbering an Interval's Candles from the newest (Newest = 1).
        // Intervals are counted in minutes from 1970-01-05, a Monday midnight, so weeks start on Monday.
        var row = await db.Database
            .SqlQueryRaw<AverageRow>(
                """
                SELECT AVG([Close]) AS [Average], COUNT(*) AS [Count], MIN(OpenTime) AS FirstOpenTime, MAX(OpenTime) AS LastOpenTime
                FROM (
                    SELECT OpenTime, [Close],
                        ROW_NUMBER() OVER (
                            PARTITION BY DATEDIFF_BIG(minute, '1970-01-05', OpenTime) / @periodMinutes
                            ORDER BY OpenTime DESC) AS Newest
                    FROM Candles
                    WHERE Symbol = @symbol AND OpenTime >= @from AND OpenTime <= @through
                ) AS numbered
                WHERE Newest = 1
                HAVING COUNT(*) > 0
                """,
                new SqlParameter("periodMinutes", (long)period.Length.TotalMinutes),
                new SqlParameter("symbol", SqlDbType.VarChar, 20) { Value = symbol.Value },
                new SqlParameter("from", SqlDbType.DateTime2) { Value = from },
                new SqlParameter("through", SqlDbType.DateTime2) { Value = through })
            .SingleOrDefaultAsync(cancellationToken);

        return ToAveragePrice(symbol, row);
    }

    private static AveragePrice? ToAveragePrice(SymbolCode symbol, AverageRow? row) =>
        row is null
            ? null
            : new AveragePrice(
                symbol,
                AsUtc(row.FirstOpenTime),
                AsUtc(row.LastOpenTime) + Candle.Length,
                row.Average,
                row.Count);

    private static DateTimeOffset AsUtc(DateTime stored) => new(DateTime.SpecifyKind(stored, DateTimeKind.Utc));

    private sealed class AverageRow
    {
        public decimal Average { get; set; }

        public int Count { get; set; }

        public DateTime FirstOpenTime { get; set; }

        public DateTime LastOpenTime { get; set; }
    }
}
