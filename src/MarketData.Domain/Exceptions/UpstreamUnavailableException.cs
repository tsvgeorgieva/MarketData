namespace MarketData.Domain.Exceptions;

/// <summary>An expected failure: a service we depend on cannot be reached. The API reports it as 503.</summary>
public sealed class UpstreamUnavailableException(string message) : Exception(message);
