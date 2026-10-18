namespace MarketData.Domain.Exceptions;

/// <summary>An expected failure: the request is well formed but cannot be acted on. The API reports it as 422.</summary>
public sealed class UnprocessableException(string message) : Exception(message);
