namespace MarketData.Application.Symbols.Options;

public sealed class SymbolsOptions
{
    public const string SectionName = "Symbols";

    /// <summary>The Monitored Symbols of the very first run; later changes are made at runtime.</summary>
    public IReadOnlyList<string> Seed { get; set; } = [];

    /// <summary>The most Symbols that may be monitored at once; adding one more is refused.</summary>
    public int MaxMonitored { get; set; } = 20;
}
