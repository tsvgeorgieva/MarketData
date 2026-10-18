using MarketData.Application.Symbols;
using MarketData.Domain.Symbols;
using Microsoft.EntityFrameworkCore;

namespace MarketData.Infrastructure.Persistence;

internal sealed class MonitoredSymbolRepository(IDbContextFactory<MarketDataDbContext> dbFactory) : IMonitoredSymbolRepository
{
    public async Task<IReadOnlyList<SymbolCode>> ListMonitoredAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.MonitoredSymbols
            .Where(monitored => monitored.RemovedAt == null)
            .OrderBy(monitored => monitored.Symbol)
            .Select(monitored => monitored.Symbol)
            .ToListAsync(cancellationToken);
    }
}
