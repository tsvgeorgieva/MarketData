using System.Net.Http.Headers;
using System.Text.Json;
using MarketData.Client.Output;

namespace MarketData.Client.Api;

/// <summary>Does the HTTP and the serialisation; knows nothing of commands or terminal output.</summary>
internal sealed class ApiClient(HttpClient http) : IApiClient
{
    public Task<string> GetRawAsync(string path, OutputFormat format, CancellationToken cancellationToken) =>
        SendAsync(path, format == OutputFormat.Xml ? "application/xml" : "application/json", cancellationToken);

    public async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken) =>
        JsonSerializer.Deserialize<T>(await SendAsync(path, "application/json", cancellationToken), JsonSerializerOptions.Web)!;

    private async Task<string> SendAsync(string path, string mediaType, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return response.IsSuccessStatusCode
                ? body
                : throw new ApiException((int)response.StatusCode, response.ReasonPhrase ?? "", response.Content.Headers.ContentType?.MediaType, body);
        }
        catch (HttpRequestException exception)
        {
            throw new ServerUnreachableException(http.BaseAddress!, exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ServerUnreachableException(http.BaseAddress!, exception);
        }
    }
}
