using System.Globalization;
using MarketData.Domain.Exceptions;
using MarketData.Domain.Prices;

namespace MarketData.UnitTests.Domain;

public class PeriodTests
{
    [Theory]
    [InlineData("1M")]
    [InlineData("5M")]
    [InlineData("1W")]
    [InlineData("2h")]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_UnknownOrDifferentlyCasedCode_ThrowsInvalidInput(string? code)
    {
        Assert.Throws<InvalidInputException>(() => Period.Parse(code));
    }

    [Theory]
    [InlineData("1m", "2026-01-02T12:03:45Z", "2026-01-02T12:03:00Z")]
    [InlineData("5m", "2026-01-02T12:04:59Z", "2026-01-02T12:00:00Z")]
    [InlineData("30m", "2026-01-02T12:31:00Z", "2026-01-02T12:30:00Z")]
    [InlineData("1d", "2026-01-02T23:59:59Z", "2026-01-02T00:00:00Z")]
    [InlineData("1w", "2026-01-04T23:59:59Z", "2025-12-29T00:00:00Z")]
    [InlineData("1w", "2025-12-29T00:00:00Z", "2025-12-29T00:00:00Z")]
    public void StartOfIntervalContaining_AlignsToUtcAndWeeksToMonday(string code, string instant, string expected)
    {
        var period = Period.Parse(code);

        Assert.Equal(Utc(expected), period.StartOfIntervalContaining(Utc(instant)));
    }

    [Theory]
    [InlineData("5m", "2026-01-02T12:04:59Z", "2026-01-02T12:05:00Z")]
    [InlineData("1d", "2026-01-02T12:04:00Z", "2026-01-03T00:00:00Z")]
    [InlineData("1w", "2026-01-02T12:04:00Z", "2026-01-05T00:00:00Z")]
    public void EndOfIntervalContaining_IsTheStartOfTheNextInterval(string code, string instant, string expected)
    {
        var period = Period.Parse(code);

        Assert.Equal(Utc(expected), period.EndOfIntervalContaining(Utc(instant)));
    }

    private static DateTime Utc(string text) =>
        DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
}
