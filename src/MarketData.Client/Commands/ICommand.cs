using MarketData.Client.Api;
using MarketData.Client.Output;

namespace MarketData.Client.Commands;

/// <summary>One thing the client can do, such as <c>avg24h</c>; returns the text to print.</summary>
internal interface ICommand
{
    Task<string> RunAsync(IApiClient api, OutputFormat format, CancellationToken cancellationToken);
}
