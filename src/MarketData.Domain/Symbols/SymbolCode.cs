using System.Diagnostics.CodeAnalysis;

namespace MarketData.Domain.Symbols;

/// <summary>A Symbol's code such as BTCUSDT: trimmed, uppercase, 3 to 20 letters or digits.</summary>
public sealed record SymbolCode
{
    public const int MinLength = 3;
    public const int MaxLength = 20;

    private SymbolCode(string value) => Value = value;

    public string Value { get; }

    public static bool TryParse(string? text, [NotNullWhen(true)] out SymbolCode? code)
    {
        code = null;
        var value = text?.Trim().ToUpperInvariant();
        if (value is null || value.Length is < MinLength or > MaxLength || !value.All(char.IsAsciiLetterOrDigit))
        {
            return false;
        }

        code = new SymbolCode(value);
        return true;
    }

    public static SymbolCode Parse(string? text) =>
        TryParse(text, out var code)
            ? code
            : throw new FormatException($"A symbol code is {MinLength} to {MaxLength} letters or digits.");

    public override string ToString() => Value;
}
