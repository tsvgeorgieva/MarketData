using MarketData.Domain.Candles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarketData.Infrastructure.Persistence;

internal sealed class CandleConfiguration : IEntityTypeConfiguration<Candle>
{
    public void Configure(EntityTypeBuilder<Candle> builder)
    {
        builder.ToTable("Candles");
        builder.HasKey(candle => new { candle.Symbol, candle.OpenTime });

        builder.Property(candle => candle.Symbol)
            .HasConversion(Conversions.SymbolCode)
            .HasMaxLength(20)
            .IsUnicode(false);
        builder.Property(candle => candle.OpenTime)
            .HasConversion(Conversions.Instant)
            .HasColumnType("datetime2(3)");

        builder.Property(candle => candle.Open).HasPrecision(18, 8);
        builder.Property(candle => candle.High).HasPrecision(18, 8);
        builder.Property(candle => candle.Low).HasPrecision(18, 8);
        builder.Property(candle => candle.Close).HasPrecision(18, 8);
        builder.Property(candle => candle.Volume).HasPrecision(28, 8);
    }
}
