using MarketData.Application.Symbols;

namespace MarketData.Api.Ingestion;

internal sealed class SymbolChangeHostedService(ISymbolChangeEventHandler symbolChangeEventHandler) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => symbolChangeEventHandler.RunAsync(stoppingToken);
}
