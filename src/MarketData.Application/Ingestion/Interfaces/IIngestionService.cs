namespace MarketData.Application.Ingestion.Interfaces;

public interface IIngestionService
{
    /// <summary>Reads closed Candles from Binance into the queue until the stream ends or <paramref name="stoppingToken"/> is cancelled.</summary>
    Task RunAsync(CancellationToken stoppingToken);
}
