using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;
using Microsoft.Extensions.Logging;

namespace MarketData.Application.Ingestion;

/// <summary>
/// Reads closed one-minute Candles for every Monitored Symbol from one Binance stream into the queue, and follows Symbols added or removed
/// while it runs by subscribing or unsubscribing on the open connection.
/// </summary>
public sealed class IngestionService(
    IMonitoredSymbolRepository monitoredSymbolRepository,
    IBinanceStreamFactory binanceStreamFactory,
    CandleQueue candleQueue,
    SymbolChanges symbolChanges,
    ILogger<IngestionService> logger) : IIngestionService
{
    public async Task RunAsync(CancellationToken stoppingToken)
    {
        try
        {
            await CollectAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Candle collection stopped");
        }
    }

    private async Task CollectAsync(CancellationToken stoppingToken)
    {
        var monitored = await WaitForMonitoredSymbolsAsync(stoppingToken);

        await using var stream = await binanceStreamFactory.ConnectAsync(monitored, stoppingToken);
        logger.LogInformation("Collecting closed candles for {Count} symbols", monitored.Count);

        // Reading and following Symbol changes run side by side; whichever ends first ends the connection.
        using var connectionStop = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var reading = ReadAsync(stream, connectionStop.Token);
        var following = FollowChangesAsync(stream, [.. monitored], connectionStop.Token);

        await Task.WhenAny(reading, following);
        await connectionStop.CancelAsync();
        await SettleAsync(reading, connectionStop.Token);
        await SettleAsync(following, connectionStop.Token);

        logger.LogWarning("The Binance stream ended; collection has stopped");
    }

    /// <summary>Waits for the task to end, ignoring the cancellation that ended it; any other failure is thrown.</summary>
    private static async Task SettleAsync(Task task, CancellationToken connectionStopped)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException) when (connectionStopped.IsCancellationRequested)
        {
        }
    }

    /// <summary>The Monitored Symbols; with none, waits until a Symbol is added, because a connection needs at least one.</summary>
    private async Task<List<SymbolCode>> WaitForMonitoredSymbolsAsync(CancellationToken stoppingToken)
    {
        while (true)
        {
            var monitored = (await monitoredSymbolRepository.GetAllAsync(stoppingToken)).Select(symbol => symbol.Symbol).ToList();
            if (monitored.Count > 0)
            {
                return monitored;
            }

            logger.LogWarning("There are no Monitored Symbols; waiting for one to be added");
            await foreach (var change in symbolChanges.ReadAllAsync(stoppingToken))
            {
                if (change.Added)
                {
                    break;
                }
            }
        }
    }

    private async Task ReadAsync(IBinanceStream stream, CancellationToken cancellationToken)
    {
        await foreach (var candle in stream.ReadClosedAsync(cancellationToken))
        {
            // The open stream can still carry a Symbol removed after it was subscribed; its Candles are not collected.
            if (await monitoredSymbolRepository.ExistsAsync(candle.Symbol, cancellationToken))
            {
                candleQueue.Enqueue(candle);
            }
        }
    }

    private async Task FollowChangesAsync(IBinanceStream stream, HashSet<SymbolCode> subscribed, CancellationToken cancellationToken)
    {
        await foreach (var change in symbolChanges.ReadAllAsync(cancellationToken))
        {
            // A change for a Symbol already in that state (for example one added just before connecting) needs no call to Binance.
            if (change.Added && subscribed.Add(change.Symbol))
            {
                await stream.SubscribeAsync([change.Symbol], cancellationToken);
                logger.LogInformation("Subscribed to {Symbol}", change.Symbol);
            }
            else if (!change.Added && subscribed.Remove(change.Symbol))
            {
                await stream.UnsubscribeAsync([change.Symbol], cancellationToken);
                logger.LogInformation("Unsubscribed from {Symbol}", change.Symbol);
            }
        }
    }
}
