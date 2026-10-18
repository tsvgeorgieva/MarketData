using System.Text.Json;
using MarketData.Domain.Candles;
using MarketData.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MarketData.Infrastructure.Persistence.Repositories;

internal sealed class CandleRepository(IDbContextFactory<MarketDataDbContext> dbFactory) : ICandleRepository
{
    public async Task<int> InsertMissingAsync(IReadOnlyCollection<Candle> candles, CancellationToken cancellationToken)
    {
        var rows = candles
            .DistinctBy(candle => (candle.Symbol, candle.OpenTime))
            .Select(candle => new
            {
                symbol = candle.Symbol.Value,
                openTime = candle.OpenTime.UtcDateTime,
                open = candle.Open,
                high = candle.High,
                low = candle.Low,
                close = candle.Close,
                volume = candle.Volume,
            })
            .ToList();
        if (rows.Count == 0)
        {
            return 0;
        }

        var json = JsonSerializer.Serialize(rows);

        // One statement adds only the missing rows; the locks keep the live writer and a backfill from inserting the same row twice.
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO Candles (Symbol, OpenTime, [Open], High, Low, [Close], Volume)
            SELECT incoming.Symbol, incoming.OpenTime, incoming.[Open], incoming.High, incoming.Low, incoming.[Close], incoming.Volume
            FROM OPENJSON({json}) WITH (
                Symbol varchar(20) '$.symbol',
                OpenTime datetime2(3) '$.openTime',
                [Open] decimal(18,8) '$.open',
                High decimal(18,8) '$.high',
                Low decimal(18,8) '$.low',
                [Close] decimal(18,8) '$.close',
                Volume decimal(28,8) '$.volume') AS incoming
            WHERE NOT EXISTS (
                SELECT 1 FROM Candles WITH (UPDLOCK, HOLDLOCK)
                WHERE Candles.Symbol = incoming.Symbol AND Candles.OpenTime = incoming.OpenTime)
            """,
            cancellationToken);
    }
}
