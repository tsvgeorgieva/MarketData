namespace MarketData.Application.Ingestion.Interfaces;

public interface ICandleWriterService
{
    /// <summary>Runs until <paramref name="stoppingToken"/> is cancelled, then saves what is still queued, without retrying.</summary>
    Task RunAsync(CancellationToken stoppingToken);
}
