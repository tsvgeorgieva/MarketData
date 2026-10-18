using MarketData.Domain.Symbols;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarketData.Infrastructure.Persistence;

internal sealed class MonitoredSymbolConfiguration : IEntityTypeConfiguration<MonitoredSymbol>
{
    public void Configure(EntityTypeBuilder<MonitoredSymbol> builder)
    {
        builder.ToTable("MonitoredSymbols");
        builder.HasKey(monitored => monitored.Symbol);

        builder.Property(monitored => monitored.Symbol)
            .HasConversion(Conversions.SymbolCode)
            .HasMaxLength(20)
            .IsUnicode(false);
        builder.Property(monitored => monitored.AddedAt)
            .HasConversion(Conversions.Instant)
            .HasColumnType("datetime2(3)");
        builder.Property(monitored => monitored.RemovedAt)
            .HasConversion(Conversions.Instant)
            .HasColumnType("datetime2(3)");
    }
}
