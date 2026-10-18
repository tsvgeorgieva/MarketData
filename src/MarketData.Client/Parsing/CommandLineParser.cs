using MarketData.Client.Commands;
using MarketData.Client.Output;

namespace MarketData.Client.Parsing;

/// <summary>Reads <c>&lt;command&gt; &lt;symbol&gt; [options]</c>, handles <c>--url</c> and <c>--format</c>, and leaves the rest to the command.</summary>
internal sealed class CommandLineParser(IReadOnlyCollection<ICommandFactory> factories)
{
    public const string Usage = """
        usage: marketdata avg24h <symbol> [options]
               marketdata sma <symbol> -n <count> -p <1m|5m|30m|1d|1w> [-e <end>] [options]
        options: --url <base url>   --format <text|json|xml>
        """;

    public CommandLine Parse(IReadOnlyList<string> args)
    {
        var positional = new List<string>();
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Count; i++)
        {
            if (!args[i].StartsWith('-'))
            {
                positional.Add(args[i]);
                continue;
            }

            options[args[i]] = i + 1 < args.Count ? args[++i] : throw new UsageException($"option {args[i]} needs a value");
        }

        if (positional.Count != 2)
        {
            throw new UsageException("expected a command and a symbol");
        }

        var factory = factories.FirstOrDefault(f => f.Name == positional[0]) ?? throw new UsageException($"unknown command {positional[0]}");
        var arguments = new CommandArguments(positional[1], options);
        var url = arguments.Take("--url");
        var format = ParseFormat(arguments.Take("--format"));
        var command = factory.Create(arguments);
        arguments.EnsureNoneLeft();
        return new CommandLine(command, url, format);
    }

    private static OutputFormat ParseFormat(string? format) => format switch
    {
        null or "text" => OutputFormat.Text,
        "json" => OutputFormat.Json,
        "xml" => OutputFormat.Xml,
        _ => throw new UsageException($"unknown format {format}"),
    };
}
