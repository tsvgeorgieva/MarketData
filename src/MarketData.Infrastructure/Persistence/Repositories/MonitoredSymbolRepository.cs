using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;
using Microsoft.EntityFrameworkCore;

namespace MarketData.Infrastructure.Persistence.Repositories;

internal sealed class MonitoredSymbolRepository(IDbContextFactory<MarketDataDbContext> dbFactory) : IMonitoredSymbolRepository
{
    public async Task<IReadOnlyList<MonitoredSymbol>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.MonitoredSymbols
            .AsNoTracking()
            .Where(monitored => monitored.RemovedAt == null)
            .OrderBy(monitored => monitored.Symbol)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(SymbolCode symbol, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.MonitoredSymbols.AnyAsync(monitored => monitored.Symbol == symbol && monitored.RemovedAt == null, cancellationToken);
    }

    public async Task RemoveAsync(SymbolCode symbol, DateTimeOffset removedAt, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.MonitoredSymbols
            .Where(monitored => monitored.Symbol == symbol && monitored.RemovedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(monitored => monitored.RemovedAt, (DateTimeOffset?)removedAt), cancellationToken);
    }
}
