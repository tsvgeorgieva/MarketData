namespace MarketData.Client.Api;

/// <summary>No answer came back from the server.</summary>
internal sealed class ServerUnreachableException(Uri address, Exception inner) : Exception($"cannot reach {address}: {inner.Message}", inner);
