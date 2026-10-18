using MarketData.Application.Ingestion;

namespace MarketData.Api.Ingestion;

internal sealed class CandleWriterHostedService(ICandleWriterService candleWriterService) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => candleWriterService.RunAsync(stoppingToken);
}
