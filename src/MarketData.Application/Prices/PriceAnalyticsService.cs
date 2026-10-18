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

    private static readonly TimeSpan Day = TimeSpan.FromHours(24);

    public async Task<AveragePrice> GetAveragePrice24hAsync(string symbolText, CancellationToken cancellationToken)
    {
        var symbol = SymbolCode.TryParse(symbolText, out var parsed)
            ? parsed
            : throw new InvalidInputException($"The symbol must be {SymbolCode.MinLength} to {SymbolCode.MaxLength} letters or digits.");

        await EnsureMonitoredAsync(symbol, cancellationToken);

        var now = clock.GetUtcNow().UtcDateTime;

        // The 1440 one-minute Candles up to the last closed one, e.g. at 12:03:30: 12:03 yesterday to 12:02 today.
        // Every one-minute Candle is the last one of its own Interval, so this is the plain mean of those Candles.
        var queryEnd = Candle.OpenTimeOfLastCandleBefore(now);
        var queryStart = queryEnd + Candle.Length - Day;

        return await candlePriceAnalyticsRepository.GetAveragePriceOfLastCandlesPerPeriodAsync(symbol, Period.Minute, queryStart, queryEnd, cancellationToken)
            ?? throw new NotFoundException($"No data collected yet for {symbol} in the last 24 hours.");
    }

    public async Task<SimpleMovingAverage> GetSimpleMovingAverageAsync(SimpleMovingAverageRequest request, CancellationToken cancellationToken)
    {
        await EnsureMonitoredAsync(request.Symbol, cancellationToken);

        var now = clock.GetUtcNow().UtcDateTime;

        // Omitted or in the future means now.
        var end = request.End < now ? request.End.Value : now;

        // Query range for the last n Intervals, e.g. end = 12:03, p = 5m, n = 3: from 11:50 to 12:02.
        // It ends at the last closed Candle (12:02), which is in the newest Interval (12:00 to 12:05, one of the n).
        // It starts n - 1 whole Intervals before that one (11:50); counting back from end or the Candle would cut an Interval in half.
        var queryEnd = Candle.OpenTimeOfLastCandleBefore(end);
        var lastIntervalStart = request.Period.StartOfIntervalContaining(queryEnd);
        var queryStart = lastIntervalStart - ((request.N - 1) * request.Period.Length);

        var average = await candlePriceAnalyticsRepository.GetAveragePriceOfLastCandlesPerPeriodAsync(request.Symbol, request.Period, queryStart, queryEnd, cancellationToken)
            ?? throw new NotFoundException($"No data collected for {request.Symbol} in the window of {request.N} x {request.Period}.");

        // Still forming when the newest Candle's Interval ends after the last closed minute (never true for 1m, or when the last Interval has no Candles).
        var newestCandleOpenTime = (average.To - Candle.Length).UtcDateTime;
        var newestIntervalEnd = request.Period.EndOfIntervalContaining(newestCandleOpenTime);
        var inProgress = newestIntervalEnd > queryEnd + Candle.Length;

        // The window starts at the start of the oldest Interval used (the unit being averaged) and ends when the last Candle used closed
        // (how far the data really reaches). It does not end at the end of the newest Interval, which may still be forming.
        return new SimpleMovingAverage(
            request.Symbol,
            request.Period,
            request.N,
            request.Period.StartOfIntervalContaining(average.From.UtcDateTime),
            average.To.UtcDateTime,
            average.Average,
            average.DataPoints,
            inProgress);
    }

    private async Task EnsureMonitoredAsync(SymbolCode symbol, CancellationToken cancellationToken)
    {
        if (!await monitoredSymbolRepository.ExistsAsync(symbol, cancellationToken))
        {
            throw new NotFoundException($"{symbol} is not a monitored symbol.");
        }
    }
}
