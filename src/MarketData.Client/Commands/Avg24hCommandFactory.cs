using MarketData.Client.Parsing;

namespace MarketData.Client.Commands;

internal sealed class Avg24hCommandFactory : ICommandFactory
{
    public string Name => "avg24h";

    public ICommand Create(CommandArguments arguments) => new Avg24hCommand(arguments.Symbol);
}
