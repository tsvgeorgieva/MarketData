using MarketData.Client.Output;

namespace MarketData.Client.Api;

/// <summary>Reads from the Market Data API.</summary>
internal interface IApiClient
{
    /// <summary>The response body exactly as the API sent it, in JSON or XML.</summary>
    Task<string> GetRawAsync(string path, OutputFormat format, CancellationToken cancellationToken);

    /// <summary>The response read into its contract type.</summary>
    Task<T> GetAsync<T>(string path, CancellationToken cancellationToken);
}
