using MarketData.Domain.Exceptions;

namespace MarketData.Domain.Prices;

/// <summary>The length of one data point of a moving average: 1m, 5m, 30m, 1d or 1w, aligned to UTC (days at 00:00, weeks on Monday 00:00).</summary>
public sealed record Period
{
    public static readonly Period Minute = new("1m", TimeSpan.FromMinutes(1));
    public static readonly Period FiveMinutes = new("5m", TimeSpan.FromMinutes(5));
    public static readonly Period ThirtyMinutes = new("30m", TimeSpan.FromMinutes(30));
    public static readonly Period Day = new("1d", TimeSpan.FromDays(1));
    public static readonly Period Week = new("1w", TimeSpan.FromDays(7));

    private static readonly Period[] All = [Minute, FiveMinutes, ThirtyMinutes, Day, Week];

    private Period(string code, TimeSpan length)
    {
        Code = code;
        Length = length;
    }

    public string Code { get; }

    public TimeSpan Length { get; }

    /// <summary>Case-sensitive: only the exact codes are accepted.</summary>
    public static Period Parse(string? text) =>
        All.FirstOrDefault(period => period.Code == text)
            ?? throw new InvalidInputException("Parameter 'p' must be one of 1m, 5m, 30m, 1d, 1w (lower case).");

    public override string ToString() => Code;

    /// <summary>
    /// The start of the Interval containing <paramref name="instant"/>: the time rounded down to a multiple of the Period.
    /// Ticks count from 0001-01-01, a Monday midnight, so every Period divides them evenly: days start at 00:00 UTC and weeks on Monday.
    /// Examples: 5m at 12:04:59 gives 12:00:00 (the 4m59s remainder is subtracted); 1d at Fri 2026-01-02 12:04 gives 2026-01-02 00:00;
    /// 1w at the same time gives Mon 2025-12-29 00:00, the Monday that started that week.
    /// </summary>
    public DateTime StartOfIntervalContaining(DateTime instant) =>
        new(instant.Ticks - (instant.Ticks % Length.Ticks), DateTimeKind.Utc);

    /// <summary>The end of the Interval containing <paramref name="instant"/>, which is the start of the next one. Example: 5m at 12:04:59 gives 12:05:00.</summary>
    public DateTime EndOfIntervalContaining(DateTime instant) => StartOfIntervalContaining(instant) + Length;
}
