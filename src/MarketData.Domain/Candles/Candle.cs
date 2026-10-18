using MarketData.Domain.Symbols;

namespace MarketData.Domain.Candles;

/// <summary>A closed one-minute summary of a Symbol's trading, identified by Symbol and open time.</summary>
public sealed record Candle(
    SymbolCode Symbol,
    DateTimeOffset OpenTime,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume)
{
    public static readonly TimeSpan Length = TimeSpan.FromMinutes(1);

    /// <summary>
    /// When the last Candle that had closed by <paramref name="time"/> opened. At 12:03:30 that is 12:02, and at exactly 12:03:00 it is also 12:02
    /// (a Candle that closes at <paramref name="time"/> counts).
    /// </summary>
    public static DateTime OpenTimeOfLastCandleBefore(DateTime time)
    {
        // Round down to the start of the minute the time falls in; that minute's Candle is still forming (or has just opened).
        var currentMinuteStart = new DateTime(time.Ticks - (time.Ticks % Length.Ticks), DateTimeKind.Utc);

        // The Candle before it is the last one that has closed.
        return currentMinuteStart - Length;
    }
}
