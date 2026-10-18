using MarketData.Domain.Exceptions;
using MarketData.Domain.Symbols;

namespace MarketData.Domain.Prices;

/// <summary>A valid request for a Simple Moving Average: <see cref="N"/> Intervals of <see cref="Period"/>, ending at <see cref="End"/>.</summary>
public sealed class SimpleMovingAverageRequest
{
    public const int MaxN = 1000;

    /// <summary>Checks the parts in order (symbol, <paramref name="n"/>, <paramref name="period"/>) and throws <see cref="InvalidInputException"/> naming the first bad one.</summary>
    /// <param name="end">A UTC time; null means now.</param>
    public SimpleMovingAverageRequest(string symbol, int n, string period, DateTime? end)
    {
        Symbol = SymbolCode.TryParse(symbol, out var parsed)
            ? parsed
            : throw new InvalidInputException($"The symbol must be {SymbolCode.MinLength} to {SymbolCode.MaxLength} letters or digits.");

        N = n is >= 1 and <= MaxN
            ? n
            : throw new InvalidInputException($"Parameter 'n' must be a whole number from 1 to {MaxN}.");

        Period = Period.Parse(period);
        End = end;
    }

    public SymbolCode Symbol { get; }

    public int N { get; }

    public Period Period { get; }

    public DateTime? End { get; }
}
