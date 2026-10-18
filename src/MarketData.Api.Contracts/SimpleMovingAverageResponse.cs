using System.Xml.Serialization;

namespace MarketData.Api.Contracts;

/// <summary>The Simple Moving Average and the window it actually covers.</summary>
[XmlRoot("simpleMovingAverage")]
public sealed class SimpleMovingAverageResponse
{
    [XmlElement("symbol")]
    public string Symbol { get; set; } = "";

    /// <summary>The length of one data point: 1m, 5m, 30m, 1d or 1w.</summary>
    [XmlElement("period")]
    public string Period { get; set; } = "";

    /// <summary>How many data points were asked for (<c>n</c>).</summary>
    [XmlElement("requested")]
    public int Requested { get; set; }

    /// <summary>When the oldest data point used starts (UTC).</summary>
    [XmlElement("from")]
    public DateTime From { get; set; }

    /// <summary>When the last Candle used closed (UTC).</summary>
    [XmlElement("to")]
    public DateTime To { get; set; }

    /// <summary>The mean Price, rounded to eight decimal places.</summary>
    [XmlElement("average")]
    public decimal Average { get; set; }

    /// <summary>How many data points the average uses; fewer than requested when some periods have no data.</summary>
    [XmlElement("dataPoints")]
    public int DataPoints { get; set; }

    /// <summary>True when the newest data point is a period that has not finished yet.</summary>
    [XmlElement("inProgress")]
    public bool InProgress { get; set; }
}
