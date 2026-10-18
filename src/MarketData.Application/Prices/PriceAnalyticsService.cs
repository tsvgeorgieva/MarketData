using MarketData.Domain.Candles;
using MarketData.Domain.Exceptions;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Prices;
using MarketData.Domain.Symbols;

namespace MarketData.Application.Prices;

public sealed class PriceAnalyticsService(
    IMonitoredSymbolRepository monitoredSymbolRepository,
    ICandlePriceAnalyticsRepository candlePriceAnalyticsRepository,
    TimeProvider clock) : IPriceAnalyticsService
{
    private const int MaxDataPoints = 1000;
    private const int AveragePlaces = 8;

    private static readonly TimeSpan Day = TimeSpan.FromHours(24);


    public async Task<AveragePrice> GetAveragePrice24hAsync(string symbolText, CancellationToken cancellationToken)
    {
        var symbol = ParseSymbol(symbolText);
        await EnsureMonitoredAsync(symbol, cancellationToken);

        var now = clock.GetUtcNow();
        return await candlePriceAnalyticsRepository.GetAveragePriceAsync(symbol, now - Day, now, cancellationToken)
            ?? throw new NotFoundException($"No data collected yet for {symbol} in the last 24 hours.");
    }

    public async Task<SimpleMovingAverage> GetSimpleMovingAverageAsync(string symbolText, int n, string p, DateTime? e, CancellationToken cancellationToken)
    {
        ValidateN(n);

        var symbol = ParseSymbol(symbolText);
        await EnsureMonitoredAsync(symbol, cancellationToken);

        var period = Period.Parse(p);

        var now = clock.GetUtcNow().UtcDateTime;
        var end = CalculateEnd(e, now);

        var (lastCandleOpen, newestBucket, windowStart) = CalculateWindow(period, n, end);

        var candles = await candlePriceAnalyticsRepository.GetLastCandlesPerPeriodAsync(symbol, period, windowStart, lastCandleOpen, cancellationToken);
        if (candles.Count == 0)
        {
            throw new NotFoundException($"No data collected for {symbol} in the window of {n} x {period}.");
        }

        var newest = candles[^1];
        var inProgress = period != Period.Minute
            && BucketStart(period, newest.OpenTime.UtcDateTime) == newestBucket
            && newestBucket + period.Length > lastCandleOpen + Candle.Length;

        return new SimpleMovingAverage(
            symbol,
            period,
            n,
            BucketStart(period, candles[0].OpenTime.UtcDateTime),
            newest.OpenTime.UtcDateTime + Candle.Length,
            Math.Round(candles.Average(candle => candle.Close), AveragePlaces),
            candles.Count,
            inProgress);
    }

    private static SymbolCode ParseSymbol(string symbolText) =>
        SymbolCode.TryParse(symbolText, out var symbol)
            ? symbol
            : throw new InvalidInputException($"The symbol must be {SymbolCode.MinLength} to {SymbolCode.MaxLength} letters or digits.");

    private async Task EnsureMonitoredAsync(SymbolCode symbol, CancellationToken cancellationToken)
    {
        if (!await monitoredSymbolRepository.ExistsAsync(symbol, cancellationToken))
        {
            throw new NotFoundException($"{symbol} is not a monitored symbol.");
        }
    }

    private static void ValidateN(int n)
    {
        if (n is < 1 or > MaxDataPoints)
        {
            throw new InvalidInputException($"Parameter 'n' must be a whole number from 1 to {MaxDataPoints}.");
        }
    }

    /// <summary>The end of the window, a UTC time: omitted or in the future means now.</summary>
    private static DateTime CalculateEnd(DateTime? e, DateTime now)
    {
        var end = e ?? now;
        return end < now ? end : now;
    }

    /// <summary>
    /// The window of <paramref name="n"/> Buckets ending at <paramref name="end"/>. Only Candles that had closed by then count, so the newest Bucket is the one
    /// holding the last closed minute. Buckets are aligned to the clock, so the window starts at the start of the Bucket <c>n - 1</c> before it, not at <c>end - n * period</c>.
    /// </summary>
    private static (DateTime LastCandleOpen, DateTime NewestBucket, DateTime WindowStart) CalculateWindow(Period period, int n, DateTime end)
    {
        var lastCandleOpen = new DateTime(end.Ticks - (end.Ticks % Candle.Length.Ticks), DateTimeKind.Utc) - Candle.Length;
        var newestBucket = BucketStart(period, lastCandleOpen);
        return (lastCandleOpen, newestBucket, newestBucket - ((n - 1) * period.Length));
    }

    /// <summary>The start of the Bucket containing <paramref name="instant"/>. Tick zero (0001-01-01) is a Monday midnight, so every Period divides the ticks evenly.</summary>
    private static DateTime BucketStart(Period period, DateTime instant) =>
        new(instant.Ticks - (instant.Ticks % period.Length.Ticks), DateTimeKind.Utc);
}
