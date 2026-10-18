using System.Xml.Serialization;

namespace MarketData.Api.Contracts;

/// <summary>A Monitored Symbol and when it was added.</summary>
public sealed class MonitoredSymbolResponse
{
    [XmlElement("symbol")]
    public string Symbol { get; set; } = "";

    /// <summary>When the Symbol was added (UTC).</summary>
    [XmlElement("addedAt")]
    public DateTime AddedAt { get; set; }
}
