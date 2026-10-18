using MarketData.Domain.Exceptions;
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

    public async Task<(MonitoredSymbol Symbol, bool Added)> AddAsync(SymbolCode symbol, DateTimeOffset addedAt, int maxMonitored, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Adds queue up on one lock that lasts until the transaction ends, so the checks below cannot be overtaken by another add.
        await db.Database.ExecuteSqlAsync(
            $"EXEC sp_getapplock @Resource = N'MonitoredSymbols.Add', @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 10000",
            cancellationToken);

        var row = await db.MonitoredSymbols.AsNoTracking().FirstOrDefaultAsync(monitored => monitored.Symbol == symbol, cancellationToken);
        if (row is { RemovedAt: null })
        {
            return (row, false);
        }

        if (await db.MonitoredSymbols.CountAsync(monitored => monitored.RemovedAt == null, cancellationToken) >= maxMonitored)
        {
            throw new ConflictException($"At most {maxMonitored} symbols can be monitored.");
        }

        var added = new MonitoredSymbol(symbol, addedAt);
        if (row is null)
        {
            db.MonitoredSymbols.Add(added);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // A Removed Symbol keeps its row, so adding it again restores that row and its Candles stay attached.
            await db.MonitoredSymbols
                .Where(monitored => monitored.Symbol == symbol)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(monitored => monitored.AddedAt, addedAt)
                        .SetProperty(monitored => monitored.RemovedAt, (DateTimeOffset?)null),
                    cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return (added, true);
    }

    public async Task RemoveAsync(SymbolCode symbol, DateTimeOffset removedAt, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.MonitoredSymbols
            .Where(monitored => monitored.Symbol == symbol && monitored.RemovedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(monitored => monitored.RemovedAt, (DateTimeOffset?)removedAt), cancellationToken);
    }
}
