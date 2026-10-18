namespace MarketData.Application.Symbols.Interfaces;

public interface ISymbolChangeEventHandler
{
    /// <summary>Applies each Symbol change to the open Binance connection until <paramref name="stoppingToken"/> is cancelled.</summary>
    Task RunAsync(CancellationToken stoppingToken);
}
