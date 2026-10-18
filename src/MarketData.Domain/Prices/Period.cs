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
}
