using MarketData.Client.Commands;
using MarketData.Client.Output;

namespace MarketData.Client.Parsing;

/// <summary>What the user asked for: a command, and where and how to show the answer.</summary>
internal sealed record CommandLine(ICommand Command, string? Url, OutputFormat Format);
