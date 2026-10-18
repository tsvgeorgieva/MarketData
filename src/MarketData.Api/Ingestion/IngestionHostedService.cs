using MarketData.Application.Ingestion;

namespace MarketData.Api.Ingestion;

internal sealed class IngestionHostedService(IIngestionService ingestion) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => ingestion.RunAsync(stoppingToken);
}
