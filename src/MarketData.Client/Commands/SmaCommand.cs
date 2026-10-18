using System.Globalization;
using MarketData.Api.Contracts;
using MarketData.Client.Api;
using MarketData.Client.Output;
using MarketData.Client.Parsing;

namespace MarketData.Client.Commands;

/// <summary><c>sma &lt;symbol&gt; -n -p [-e]</c>: the Simple Moving Average. The values go to the API as typed, which is the one that validates them.</summary>
internal sealed class SmaCommand(string symbol, string n, string p, string? e) : ICommand
{
    public async Task<string> RunAsync(IApiClient api, OutputFormat format, CancellationToken cancellationToken)
    {
        var query = $"n={Uri.EscapeDataString(n)}&p={Uri.EscapeDataString(p)}";
        if (e is not null)
        {
            query += $"&e={Uri.EscapeDataString(e)}";
        }

        var path = $"/api/{Uri.EscapeDataString(symbol)}/SimpleMovingAverage?{query}";
        if (format != OutputFormat.Text)
        {
            return await api.GetRawAsync(path, format, cancellationToken);
        }

        var average = await api.GetAsync<SimpleMovingAverageResponse>(path, cancellationToken);
        var inProgress = average.InProgress ? "\nthe newest period has not finished yet" : "";
        return string.Create(CultureInfo.InvariantCulture,
            $"{average.Symbol} simple moving average over {average.Requested} x {average.Period}: {average.Average:0.########}\nwindow: {average.From:u} to {average.To:u}\ndata points: {average.DataPoints}{inProgress}\n");
    }

    internal sealed class Factory : ICommandFactory
    {
        public string Name => "sma";

        public ICommand Create(CommandArguments arguments) =>
            new SmaCommand(arguments.Symbol, arguments.Require("-n"), arguments.Require("-p"), arguments.Take("-e"));
    }
}
