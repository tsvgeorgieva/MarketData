using MarketData.Application.Symbols;
using MarketData.Domain.Symbols;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarketData.Infrastructure.Persistence;

/// <summary>Runs first at startup: migrates when asked to, then seeds the Monitored Symbols on the very first run.</summary>
internal sealed class DatabaseInitializer(
    IDbContextFactory<MarketDataDbContext> dbFactory,
    IOptions<DatabaseOptions> database,
    IOptions<SymbolsOptions> symbols,
    TimeProvider clock,
    ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        if (database.Value.MigrateOnStartup)
        {
            await db.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Database migrated");
        }
        else
        {
            logger.LogInformation("Database migration on startup is off");
        }

        // Rows are never deleted, so an empty table means the very first run.
        if (!await db.MonitoredSymbols.AnyAsync(cancellationToken))
        {
            var now = clock.GetUtcNow();
            db.MonitoredSymbols.AddRange(symbols.Value.Seed.Select(text => new MonitoredSymbol(SymbolCode.Parse(text), now)));
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} Monitored Symbols", symbols.Value.Seed.Count);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
