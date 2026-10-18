using MarketData.Domain.Symbols;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MarketData.Infrastructure.Persistence;

internal static class Conversions
{
    public static readonly ValueConverter<SymbolCode, string> SymbolCode =
        new(code => code.Value, text => Domain.Symbols.SymbolCode.Parse(text));

    /// <summary>Instants are stored as UTC datetime2 and read back as UTC.</summary>
    public static readonly ValueConverter<DateTimeOffset, DateTime> Instant =
        new(instant => instant.UtcDateTime, stored => new DateTimeOffset(DateTime.SpecifyKind(stored, DateTimeKind.Utc)));
}
