using System.ComponentModel.DataAnnotations;

namespace MarketData.Infrastructure.Binance;

public sealed class BinanceOptions
{
    public const string SectionName = "Binance";

    /// <summary>The market-data-only WebSocket host, without a path.</summary>
    [Required, RegularExpression("^wss?://[^/]+$", ErrorMessage = "WebSocketUrl must be a ws:// or wss:// address without a path.")]
    public string WebSocketUrl { get; set; } = "";

    /// <summary>The market-data-only REST host, without a path.</summary>
    [Required, RegularExpression("^https?://[^/]+$", ErrorMessage = "RestUrl must be an http:// or https:// address without a path.")]
    public string RestUrl { get; set; } = "https://data-api.binance.vision";
}
