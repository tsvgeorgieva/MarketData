using System.Threading.Channels;
using MarketData.Domain.Candles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarketData.Application.Ingestion;

/// <summary>The single consumer of the queue: saves Candles in batches and retries when the database fails.</summary>
public sealed class CandleWriter(
    ICandleStore store,
    TimeProvider clock,
    IOptions<IngestionOptions> options,
    ILogger<CandleWriter> logger)
{
    /// <summary>Runs until the queue is completed and empty. Retries stop once <paramref name="shutdown"/> is cancelled.</summary>
    public async Task RunAsync(ChannelReader<Candle> queue, CancellationToken shutdown)
    {
        var batch = new List<Candle>(options.Value.MaxBatchSize);
        while (await queue.WaitToReadAsync(CancellationToken.None))
        {
            while (batch.Count < options.Value.MaxBatchSize && queue.TryRead(out var candle))
            {
                batch.Add(candle);
            }

            await SaveAsync(batch, shutdown);
            batch.Clear();
        }
    }

    private async Task SaveAsync(List<Candle> batch, CancellationToken shutdown)
    {
        while (true)
        {
            try
            {
                var added = await store.InsertMissingAsync(batch, CancellationToken.None);
                logger.LogDebug("Saved {Added} of {Count} candles", added, batch.Count);
                return;
            }
            catch (Exception ex) when (!shutdown.IsCancellationRequested)
            {
                logger.LogError(ex, "Saving {Count} candles failed; retrying in {Delay}", batch.Count, options.Value.WriterRetryDelay);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Shutting down; {Count} candles were not saved", batch.Count);
                return;
            }

            try
            {
                await Task.Delay(options.Value.WriterRetryDelay, clock, shutdown);
            }
            catch (OperationCanceledException)
            {
                logger.LogError("Shutting down; {Count} candles were not saved", batch.Count);
                return;
            }
        }
    }
}
