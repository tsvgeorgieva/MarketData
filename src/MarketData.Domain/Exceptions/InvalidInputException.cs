namespace MarketData.Domain.Exceptions;

/// <summary>An expected failure: the caller sent something that is not valid. The API reports it as 400.</summary>
public sealed class InvalidInputException(string message) : Exception(message);
