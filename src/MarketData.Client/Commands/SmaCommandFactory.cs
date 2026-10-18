using MarketData.Client.Parsing;

namespace MarketData.Client.Commands;

internal sealed class SmaCommandFactory : ICommandFactory
{
    public string Name => "sma";

    public ICommand Create(CommandArguments arguments) =>
        new SmaCommand(arguments.Symbol, arguments.Require("-n"), arguments.Require("-p"), arguments.Take("-e"));
}
