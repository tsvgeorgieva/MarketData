using MarketData.Application.Ingestion;
using MarketData.Application.Ingestion.Interfaces;

namespace MarketData.Api.Ingestion;

internal sealed class IngestionHostedService(IIngestionService ingestion) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => ingestion.RunAsync(stoppingToken);
}
