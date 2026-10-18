using MarketData.Client.Api;
using MarketData.Client.Commands;
using MarketData.Client.Output;
using MarketData.Client.Parsing;

namespace MarketData.Client;

/// <summary>The entry point: wires the parts together by hand and turns every outcome into an exit code.</summary>
public static class ConsoleClient
{
    public const string UrlVariable = "MARKETDATA_URL";
    private const string DefaultUrl = "http://localhost:8080";

    /// <param name="handler">Replaces the network, so tests can talk to an in-memory server.</param>
    public static async Task<int> RunAsync(
        string[] args, TextWriter output, TextWriter error, Func<string, string?> getEnvironmentVariable,
        HttpMessageHandler? handler, CancellationToken cancellationToken)
    {
        var parser = new CommandLineParser([new Avg24hCommand.Factory(), new SmaCommand.Factory()]);
        try
        {
            var line = parser.Parse(args);
            var baseUrl = ParseBaseUrl(line.Url ?? getEnvironmentVariable(UrlVariable) ?? DefaultUrl);
            using var http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
            http.BaseAddress = baseUrl;

            await output.WriteAsync(await line.Command.RunAsync(new ApiClient(http), line.Format, cancellationToken));
            return ExitCode.Ok;
        }
        catch (UsageException exception)
        {
            await error.WriteLineAsync($"error: {exception.Message}\n{CommandLineParser.Usage}");
            return ExitCode.BadUsage;
        }
        catch (ApiException exception)
        {
            await error.WriteLineAsync(ProblemSummary.Describe(exception));
            return ExitCode.ApiError;
        }
        catch (ServerUnreachableException exception)
        {
            await error.WriteLineAsync($"error: {exception.Message}");
            return ExitCode.Unreachable;
        }
    }

    private static Uri ParseBaseUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri
            : throw new UsageException($"{url} is not an http or https address");
}
