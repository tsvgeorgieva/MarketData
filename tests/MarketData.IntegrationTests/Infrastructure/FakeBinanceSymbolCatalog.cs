using MarketData.Domain.Exceptions;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;

namespace MarketData.IntegrationTests.Infrastructure;

/// <summary>The Binance REST symbol check the test controls: which Symbols are trading, and whether Binance can be reached.</summary>
public sealed class FakeBinanceSymbolCatalog : IBinanceSymbolCatalog
{
    private readonly HashSet<string> _trading = ["BTCUSDT", "ADAUSDT", "ETHUSDT", "XRPUSDT", "SOLUSDT", "DOGEUSDT"];

    public bool Unreachable { get; set; }

    public int Calls { get; private set; }

    public Task<bool> IsTradingAsync(SymbolCode symbol, CancellationToken cancellationToken)
    {
        Calls++;
        return Unreachable
            ? throw new UpstreamUnavailableException("Binance cannot be reached.")
            : Task.FromResult(_trading.Contains(symbol.Value));
    }
}
