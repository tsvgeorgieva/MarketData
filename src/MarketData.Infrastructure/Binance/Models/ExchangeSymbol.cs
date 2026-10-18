using System.Text.Json.Serialization;

namespace MarketData.Infrastructure.Binance.Models;

internal sealed class ExchangeSymbol
{
    public const string TradingStatus = "TRADING";

    [JsonPropertyName("symbol")]
    public required string Symbol { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }
}
