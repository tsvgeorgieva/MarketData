using Microsoft.EntityFrameworkCore;

namespace MarketData.Infrastructure.Persistence;

public sealed class MarketDataDbContext(DbContextOptions<MarketDataDbContext> options) : DbContext(options);
