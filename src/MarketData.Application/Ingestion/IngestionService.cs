using MarketData.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace MarketData.Application.Ingestion;

/// <summary>Reads closed one-minute Candles for every Monitored Symbol from one Binance stream into the queue.</summary>
public sealed class IngestionService(
    IMonitoredSymbolRepository monitoredSymbolRepository,
    IBinanceStreamFactory binanceStreamFactory,
    CandleQueue candleQueue,
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
        var monitored = await monitoredSymbolRepository.GetAllAsync(stoppingToken);
        if (monitored.Count == 0)
        {
            logger.LogWarning("There are no Monitored Symbols; nothing is collected");
            return;
        }

        var symbolCount = monitored.Count;
        await using var stream = await binanceStreamFactory.ConnectAsync(monitored, stoppingToken);
        logger.LogInformation("Collecting closed candles for {Count} symbols", symbolCount);

        await foreach (var candle in stream.ReadClosedAsync(stoppingToken))
        {
            // The open stream still carries a Symbol removed after it was opened; its Candles are not collected.
            if (await monitoredSymbolRepository.ExistsAsync(candle.Symbol, stoppingToken))
            {
                candleQueue.Enqueue(candle);
            }
        }

        logger.LogWarning("The Binance stream ended; collection has stopped");
    }
}
