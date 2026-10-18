namespace MarketData.Client.Parsing;

/// <summary>The command line is not one the client understands.</summary>
internal sealed class UsageException(string message) : Exception(message);
