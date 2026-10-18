using MarketData.Domain.Candles;
using MarketData.Domain.Exceptions;
using MarketData.Domain.Symbols;
using MarketData.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace MarketData.Application.Ingestion;

/// <summary>
/// Loads the last 24 hours of closed Candles of a Symbol from Binance history and queues them for the writer, which stores only the missing ones.
/// Minutes before that window stay a Gap.
/// </summary>
public sealed class BackfillService(
    IBinanceHistory binanceHistory,
    CandleIngestionQueue candleIngestionQueue,
    TimeProvider clock,
    ILogger<BackfillService> logger)
{
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    /// <summary>Queues the Symbol's closed Candles of the last 24 hours. When Binance cannot be reached it logs that and queues nothing.</summary>
    public async Task BackfillAsync(SymbolCode symbol, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var through = new DateTimeOffset(Candle.OpenTimeOfLastCandleBefore(now.UtcDateTime), TimeSpan.Zero);

        IReadOnlyList<Candle> candles;
        try
        {
            candles = await binanceHistory.GetClosedCandlesAsync(symbol, now - Window, through, cancellationToken);
        }
        catch (UpstreamUnavailableException ex)
        {
            logger.LogWarning(ex, "Backfill of {Symbol} failed; its last 24 hours stay a Gap", symbol);
            return;
        }

        foreach (var candle in candles)
        {
            candleIngestionQueue.Enqueue(candle);
        }

        logger.LogInformation("Backfilled {Count} candles for {Symbol}", candles.Count, symbol);
    }
}
