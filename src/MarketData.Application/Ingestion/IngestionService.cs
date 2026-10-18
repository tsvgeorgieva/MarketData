using MarketData.Application.Ingestion.Interfaces;
using MarketData.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace MarketData.Application.Ingestion;

/// <summary>Reads closed one-minute Candles for every Monitored Symbol from the Binance stream into the queue.</summary>
public sealed class IngestionService(
    IMonitoredSymbolRepository monitoredSymbolRepository,
    IBinanceStream binanceStream,
    CandleIngestionQueue candleIngestionQueue,
    ILogger<IngestionService> logger) : IIngestionService
{
    public async Task RunAsync(CancellationToken stoppingToken)
    {
        try
        {
            await InitializeStreamAsync(stoppingToken);
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

    /// <summary>Opens the connection for the Monitored Symbols; the stream waits for one to be added when there is none.</summary>
    private async Task InitializeStreamAsync(CancellationToken stoppingToken)
    {
        var monitored = (await monitoredSymbolRepository.GetAllAsync(stoppingToken)).Select(symbol => symbol.Symbol).ToList();
        await binanceStream.ConnectAsync(monitored, stoppingToken);
        logger.LogInformation("Connected for {Count} symbols", monitored.Count);
    }

    private async Task CollectAsync(CancellationToken stoppingToken)
    {
        await foreach (var candle in binanceStream.ReadClosedAsync(stoppingToken))
        {
            // The open stream can still carry a Symbol removed after it was subscribed; its Candles are not collected.
            if (await monitoredSymbolRepository.ExistsAsync(candle.Symbol, stoppingToken))
            {
                candleIngestionQueue.Enqueue(candle);
            }
        }

        logger.LogWarning("The Binance stream ended; collection has stopped");
    }
}
