using MarketData.Client.Parsing;

namespace MarketData.Client.Commands;

/// <summary>Knows the name and the options of one command and builds it from the command line.</summary>
internal interface ICommandFactory
{
    string Name { get; }

    /// <exception cref="UsageException">The options are missing or not valid for this command.</exception>
    ICommand Create(CommandArguments arguments);
}
