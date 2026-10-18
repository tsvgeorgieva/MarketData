namespace MarketData.Client.Parsing;

/// <summary>The symbol and the options still unclaimed; a command takes its own, and what is left over is an error.</summary>
internal sealed class CommandArguments(string symbol, Dictionary<string, string> options)
{
    public string Symbol { get; } = symbol;

    public string? Take(string option) => options.Remove(option, out var value) ? value : null;

    public string Require(string option) => Take(option) ?? throw new UsageException($"option {option} is required");

    public void EnsureNoneLeft()
    {
        if (options.Count > 0)
        {
            throw new UsageException($"unknown option {options.Keys.Order(StringComparer.Ordinal).First()}");
        }
    }
}
