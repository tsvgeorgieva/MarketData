using MarketData.Application.Ingestion;
using MarketData.Application.Ingestion.Options;
using MarketData.Domain.Symbols;
using MarketData.Application.Symbols;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketData.UnitTests.Ingestion;

public class SymbolChangesQueueTests
{
    [Fact]
    public async Task Publish_QueueFull_DropsTheOldestAndKeepsTheNewest()
    {
        var queue = new SymbolChangesQueue(Options.Create(new IngestionOptions { SymbolChangesQueueCapacity = 2 }), NullLogger<SymbolChangesQueue>.Instance);

        foreach (var symbol in new[] { "AAAUSDT", "BBBUSDT", "CCCUSDT" })
        {
            queue.Publish(new SymbolChange(SymbolCode.Parse(symbol), Added: true));
        }

        var read = new List<string>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var change in queue.ReadAllAsync(timeout.Token))
        {
            read.Add(change.Symbol.Value);
            if (read.Count == 2)
            {
                break;
            }
        }

        Assert.Equal(["BBBUSDT", "CCCUSDT"], read);
    }
}
