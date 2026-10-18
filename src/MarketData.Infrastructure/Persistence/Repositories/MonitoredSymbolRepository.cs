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

    public async Task<MonitoredSymbol?> FindAsync(SymbolCode symbol, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.MonitoredSymbols
            .AsNoTracking()
            .FirstOrDefaultAsync(monitored => monitored.Symbol == symbol && monitored.RemovedAt == null, cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.MonitoredSymbols.CountAsync(monitored => monitored.RemovedAt == null, cancellationToken);
    }

    public async Task<MonitoredSymbol> AddAsync(SymbolCode symbol, DateTimeOffset addedAt, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // A Removed Symbol keeps its row, so adding it again restores that row and its Candles stay attached.
        var restored = await db.MonitoredSymbols
            .Where(monitored => monitored.Symbol == symbol && monitored.RemovedAt != null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(monitored => monitored.AddedAt, addedAt)
                    .SetProperty(monitored => monitored.RemovedAt, (DateTimeOffset?)null),
                cancellationToken);

        var monitoredSymbol = new MonitoredSymbol(symbol, addedAt);
        if (restored == 0)
        {
            db.MonitoredSymbols.Add(monitoredSymbol);
            await db.SaveChangesAsync(cancellationToken);
        }

        return monitoredSymbol;
    }

    public async Task RemoveAsync(SymbolCode symbol, DateTimeOffset removedAt, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.MonitoredSymbols
            .Where(monitored => monitored.Symbol == symbol && monitored.RemovedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(monitored => monitored.RemovedAt, (DateTimeOffset?)removedAt), cancellationToken);
    }
}
