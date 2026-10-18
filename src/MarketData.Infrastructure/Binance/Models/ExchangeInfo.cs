using System.Text.Json.Serialization;

namespace MarketData.Infrastructure.Binance.Models;

/// <summary>The part of the exchange-info answer that says which Symbols exist and whether they trade.</summary>
internal sealed class ExchangeInfo
{
    [JsonPropertyName("symbols")]
    public required IReadOnlyList<ExchangeSymbol> Symbols { get; init; }
}
