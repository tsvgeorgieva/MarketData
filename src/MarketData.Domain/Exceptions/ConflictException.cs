namespace MarketData.Domain.Exceptions;

/// <summary>An expected failure: the request conflicts with the current state. The API reports it as 409.</summary>
public sealed class ConflictException(string message) : Exception(message);
