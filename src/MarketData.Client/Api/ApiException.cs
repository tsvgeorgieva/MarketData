namespace MarketData.Client.Api;

/// <summary>The API answered with an error status.</summary>
internal sealed class ApiException(int status, string reason, string? contentType, string body) : Exception($"{status} {reason}")
{
    public int Status { get; } = status;

    public string Reason { get; } = reason;

    public string? ContentType { get; } = contentType;

    public string Body { get; } = body;
}
