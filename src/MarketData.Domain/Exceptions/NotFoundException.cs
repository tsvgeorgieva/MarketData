namespace MarketData.Domain.Exceptions;

/// <summary>An expected failure: what was asked for does not exist. The API reports it as 404.</summary>
public sealed class NotFoundException(string message) : Exception(message);
