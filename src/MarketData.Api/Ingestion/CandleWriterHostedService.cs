using MarketData.Application.Ingestion;

namespace MarketData.Api.Ingestion;

internal sealed class CandleWriterHostedService(CandleWriter candleWriter) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => candleWriter.RunAsync(stoppingToken);
}
