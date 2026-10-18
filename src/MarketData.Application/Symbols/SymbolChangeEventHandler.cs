using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;
using Microsoft.Extensions.Logging;

namespace MarketData.Application.Symbols;

/// <summary>Keeps the Binance stream in line with the Monitored Symbols: subscribes a Symbol that was added and unsubscribes one that was removed.</summary>
public sealed class SymbolChangeEventHandler(
    SymbolChangesQueue symbolChangesQueue,
    IBinanceStream binanceStream,
    ILogger<SymbolChangeEventHandler> logger) : ISymbolChangeEventHandler
{
    public async Task RunAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var change in symbolChangesQueue.ReadAllAsync(stoppingToken))
            {
                await ApplyAsync(change, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task ApplyAsync(SymbolChange change, CancellationToken stoppingToken)
    {
        try
        {
            if (change.Added)
            {
                await binanceStream.SubscribeAsync([change.Symbol], stoppingToken);
                logger.LogInformation("Subscribed to {Symbol}", change.Symbol);
            }
            else
            {
                await binanceStream.UnsubscribeAsync([change.Symbol], stoppingToken);
                logger.LogInformation("Unsubscribed from {Symbol}", change.Symbol);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not apply the change of {Symbol} to the Binance stream", change.Symbol);
        }
    }
}
