using System.Threading.Channels;
using MarketData.Application.Symbols;
using MarketData.Domain.Candles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarketData.Application.Ingestion;

/// <summary>Collects closed one-minute Candles for every Monitored Symbol from one Binance stream.</summary>
public sealed class IngestionService(
    IMonitoredSymbolRepository symbols,
    IBinanceStreamFactory streams,
    CandleWriter writer,
    IOptions<IngestionOptions> options,
    ILogger<IngestionService> logger)
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
        var monitored = await symbols.ListMonitoredAsync(stoppingToken);
        if (monitored.Count == 0)
        {
            logger.LogWarning("There are no Monitored Symbols; nothing is collected");
            return;
        }

        var symbolCount = monitored.Count;
        var queue = Channel.CreateBounded<Candle>(
            new BoundedChannelOptions(options.Value.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true,
            },
            dropped => logger.LogWarning("Queue full; dropped the {Symbol} candle of {OpenTime}", dropped.Symbol, dropped.OpenTime));

        var writing = writer.RunAsync(queue.Reader, stoppingToken);
        try
        {
            await using var stream = await streams.ConnectAsync(monitored, stoppingToken);
            logger.LogInformation("Collecting closed candles for {Count} symbols", symbolCount);

            await foreach (var update in stream.ReadAsync(stoppingToken))
            {
                if (update.IsClosed)
                {
                    queue.Writer.TryWrite(update.Candle);
                }
            }

            logger.LogWarning("The Binance stream ended; collection has stopped");
        }
        finally
        {
            queue.Writer.Complete();
            await writing;
        }
    }
}
