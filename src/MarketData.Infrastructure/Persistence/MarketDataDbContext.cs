using MarketData.Domain.Candles;
using MarketData.Domain.Symbols;
using Microsoft.EntityFrameworkCore;

namespace MarketData.Infrastructure.Persistence;

public sealed class MarketDataDbContext(DbContextOptions<MarketDataDbContext> options) : DbContext(options)
{
    public DbSet<Candle> Candles => Set<Candle>();

    public DbSet<MonitoredSymbol> MonitoredSymbols => Set<MonitoredSymbol>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MarketDataDbContext).Assembly);
}
