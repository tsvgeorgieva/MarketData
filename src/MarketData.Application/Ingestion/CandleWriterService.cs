using MarketData.Domain.Candles;
using MarketData.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarketData.Application.Ingestion;

/// <summary>The only consumer of the queue: builds batches of Candles and saves them, retrying when the database fails.</summary>
public sealed class CandleWriterService(
    CandleQueue candleQueue,
    ICandleRepository candleRepository,
    TimeProvider clock,
    IOptions<IngestionOptions> options,
    ILogger<CandleWriterService> logger) : ICandleWriterService
{
    public async Task RunAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await candleQueue.WaitToReadAsync(stoppingToken))
            {
                await SaveBatchAsync(DequeueBatch(), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }

        await DrainQueueAfterCancellationAsync(stoppingToken);
    }

    /// <summary>Saves what is still queued once the service is stopping; a cancelled wait can leave Candles behind.</summary>
    private async Task DrainQueueAfterCancellationAsync(CancellationToken stoppingToken)
    {
        while (true)
        {
            var batch = DequeueBatch();
            if (batch.Count == 0)
            {
                return;
            }

            await SaveBatchAsync(batch, stoppingToken);
        }
    }

    private List<Candle> DequeueBatch()
    {
        var batch = new List<Candle>(options.Value.MaxBatchSize);
        while (batch.Count < options.Value.MaxBatchSize && candleQueue.TryRead(out var candle))
        {
            batch.Add(candle);
        }

        return batch;
    }

    /// <summary>Saves the batch, retrying after a delay until it succeeds or the service is stopping.</summary>
    private async Task SaveBatchAsync(List<Candle> batch, CancellationToken stoppingToken)
    {
        while (true)
        {
            if (await TrySaveBatchAsync(batch))
            {
                return;
            }

            if (stoppingToken.IsCancellationRequested)
            {
                logger.LogError("Shutting down; {Count} candles were not saved", batch.Count);
                return;
            }

            await WaitBeforeRetryAsync(stoppingToken);
        }
    }

    private async Task<bool> TrySaveBatchAsync(List<Candle> batch)
    {
        try
        {
            var added = await candleRepository.InsertMissingAsync(batch, CancellationToken.None);
            logger.LogDebug("Saved {Added} of {Count} candles", added, batch.Count);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Saving {Count} candles failed", batch.Count);
            return false;
        }
    }

    /// <summary>Waits out the retry delay; stopping the service cuts it short.</summary>
    private async Task WaitBeforeRetryAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Retrying in {Delay}", options.Value.WriterRetryDelay);
        try
        {
            await Task.Delay(options.Value.WriterRetryDelay, clock, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
