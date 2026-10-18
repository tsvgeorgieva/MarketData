using System.Xml.Serialization;

namespace MarketData.Api.Contracts;

/// <summary>The 24h Average Price and the window it actually covers.</summary>
[XmlRoot("averagePrice")]
public sealed class AveragePriceResponse
{
    [XmlElement("symbol")]
    public string Symbol { get; set; } = "";

    /// <summary>When the first Candle used opened (UTC).</summary>
    [XmlElement("from")]
    public DateTime From { get; set; }

    /// <summary>When the last Candle used closed (UTC).</summary>
    [XmlElement("to")]
    public DateTime To { get; set; }

    /// <summary>The mean Price, rounded to eight decimal places.</summary>
    [XmlElement("average")]
    public decimal Average { get; set; }

    /// <summary>How many Candles the average uses; fewer than 1,440 when less than 24 hours were collected or minutes are missing.</summary>
    [XmlElement("dataPoints")]
    public int DataPoints { get; set; }
}
