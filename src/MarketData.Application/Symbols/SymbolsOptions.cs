namespace MarketData.Application.Symbols;

public sealed class SymbolsOptions
{
    public const string SectionName = "Symbols";

    /// <summary>The Monitored Symbols of the very first run; later changes are made at runtime.</summary>
    public IReadOnlyList<string> Seed { get; set; } = [];
}
