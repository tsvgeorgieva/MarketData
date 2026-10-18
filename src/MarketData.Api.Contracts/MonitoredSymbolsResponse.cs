using System.Xml.Serialization;

namespace MarketData.Api.Contracts;

/// <summary>The Monitored Symbols; the named wrapper lets a list serialise the same way in JSON and XML.</summary>
[XmlRoot("monitoredSymbols")]
public sealed class MonitoredSymbolsResponse
{
    [XmlArray("symbols")]
    [XmlArrayItem("symbol")]
    public List<MonitoredSymbolResponse> Symbols { get; set; } = [];
}
