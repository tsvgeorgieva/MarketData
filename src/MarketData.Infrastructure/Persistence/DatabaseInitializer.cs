using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarketData.Infrastructure.Persistence;

/// <summary>Runs first at startup: migrates when asked to, then seeds the database.</summary>
internal sealed class DatabaseInitializer(
    IDbContextFactory<MarketDataDbContext> dbFactory,
    IOptions<DatabaseOptions> database,
    DatabaseSeeder seeder,
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

        await seeder.SeedAsync(db, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
