using MarketData.Application.Symbols;
using MarketData.Application.Symbols.Options;
using Microsoft.EntityFrameworkCore;
using MarketData.Domain.Symbols;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace MarketData.Infrastructure.Persistence;

/// <summary>Adds the configured Monitored Symbols on the very first run only.</summary>
internal sealed class DatabaseSeeder(
    IOptions<SymbolsOptions> symbols,
    TimeProvider clock,
    ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(MarketDataDbContext db, CancellationToken cancellationToken)
    {
        // Rows are never deleted, so an empty table means the very first run.
        if (await db.MonitoredSymbols.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = clock.GetUtcNow();
        db.MonitoredSymbols.AddRange(symbols.Value.Seed.Select(text => new MonitoredSymbol(SymbolCode.Parse(text), now)));
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} Monitored Symbols", symbols.Value.Seed.Count);
    }
}
