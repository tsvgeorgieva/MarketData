using System.Text.Json;
using System.Xml.Linq;
using MarketData.Client.Api;

namespace MarketData.Client.Output;

/// <summary>The one-line reading of an API error body, JSON or XML problem details.</summary>
internal static class ProblemSummary
{
    public static string Describe(ApiException exception)
    {
        var (detail, traceId) = Read(exception.ContentType, exception.Body);
        var message = $"error: {exception.Status} {exception.Reason}";
        if (!string.IsNullOrEmpty(detail))
        {
            message += $": {detail}";
        }

        return string.IsNullOrEmpty(traceId) ? message : $"{message} (trace id {traceId})";
    }

    private static (string? Detail, string? TraceId) Read(string? contentType, string body)
    {
        try
        {
            if (contentType?.Contains("xml", StringComparison.OrdinalIgnoreCase) == true)
            {
                var fields = XDocument.Parse(body).Descendants().ToLookup(e => e.Name.LocalName, e => e.Value);
                return (fields["detail"].FirstOrDefault(), fields["traceId"].FirstOrDefault());
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return (Property(root, "detail"), Property(root, "traceId"));
        }
        catch (Exception exception) when (exception is JsonException or System.Xml.XmlException)
        {
            return (null, null);
        }
    }

    private static string? Property(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) ? value.ToString() : null;
}
