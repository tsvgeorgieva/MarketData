using System.Globalization;
using MarketData.Api.Contracts;
using MarketData.Client.Api;
using MarketData.Client.Output;
using MarketData.Client.Parsing;

namespace MarketData.Client.Commands;

/// <summary><c>avg24h &lt;symbol&gt;</c>: the 24h Average Price.</summary>
internal sealed class Avg24hCommand(string symbol) : ICommand
{
    public async Task<string> RunAsync(IApiClient api, OutputFormat format, CancellationToken cancellationToken)
    {
        var path = $"/api/{Uri.EscapeDataString(symbol)}/24hAvgPrice";
        if (format != OutputFormat.Text)
        {
            return await api.GetRawAsync(path, format, cancellationToken);
        }

        var average = await api.GetAsync<AveragePriceResponse>(path, cancellationToken);
        return string.Create(CultureInfo.InvariantCulture,
            $"{average.Symbol} 24h average price: {average.Average:0.########}\nwindow: {average.From:u} to {average.To:u}\ndata points: {average.DataPoints}\n");
    }
}
