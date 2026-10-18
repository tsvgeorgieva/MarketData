using System.ComponentModel.DataAnnotations;

namespace MarketData.Api.Contracts;

/// <summary>The query string of the Simple Moving Average: <c>?n=&amp;p=&amp;e=</c>.</summary>
public sealed class SimpleMovingAverageRequest
{
    /// <summary>How many periods to average, 1 to 1000.</summary>
    [Required]
    public int? N { get; set; }

    /// <summary>The length of one period: 1m, 5m, 30m, 1d or 1w (lower case).</summary>
    [Required]
    public string? P { get; set; }

    /// <summary>The end of the window as a UTC time without an offset, such as 2026-01-02T12:00:00; omitted or in the future means now.</summary>
    public DateTime? E { get; set; }
}
