using MarketData.Domain.Symbols;
using Microsoft.Extensions.Options;

namespace MarketData.Application.Symbols.Options;

internal sealed class SymbolsOptionsValidator : IValidateOptions<SymbolsOptions>
{
    public ValidateOptionsResult Validate(string? name, SymbolsOptions options)
    {
        if (options.Seed.Count == 0)
        {
            return ValidateOptionsResult.Fail("Symbols:Seed must list at least one symbol.");
        }

        if (options.MaxMonitored < Math.Max(1, options.Seed.Count))
        {
            return ValidateOptionsResult.Fail("Symbols:MaxMonitored must be at least 1 and at least the number of seeded symbols.");
        }

        var codes = new List<SymbolCode>();
        foreach (var text in options.Seed)
        {
            if (!SymbolCode.TryParse(text, out var code))
            {
                return ValidateOptionsResult.Fail($"Symbols:Seed contains an invalid symbol code '{text}'.");
            }

            codes.Add(code);
        }

        return codes.Distinct().Count() == codes.Count
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Symbols:Seed lists a symbol more than once.");
    }
}
