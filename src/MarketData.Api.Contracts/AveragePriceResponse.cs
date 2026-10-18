namespace MarketData.Api.Contracts;

/// <summary>The 24h Average Price and the window it actually covers.</summary>
public sealed class AveragePriceResponse
{
    public string Symbol { get; set; } = "";

    /// <summary>When the first Candle used opened (UTC).</summary>
    public DateTime From { get; set; }

    /// <summary>When the last Candle used closed (UTC).</summary>
    public DateTime To { get; set; }

    /// <summary>The mean Price, rounded to eight decimal places.</summary>
    public decimal Average { get; set; }

    /// <summary>How many Candles the average uses; fewer than 1,440 when less than 24 hours were collected or minutes are missing.</summary>
    public int DataPoints { get; set; }
}
