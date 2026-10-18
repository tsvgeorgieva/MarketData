using MarketData.Application.Ingestion;
using MarketData.Domain.Candles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MarketData.UnitTests.Ingestion;

public class CandleQueueTests
{
    [Fact]
    public void TryRead_CandlesQueued_ReturnsThemOldestFirst()
    {
        var queue = NewQueue(capacity: 10);
        queue.Enqueue(TestCandles.At(0));
        queue.Enqueue(TestCandles.At(1));

        Assert.Equal([TestCandles.At(0), TestCandles.At(1)], Drain(queue));
    }

    [Fact]
    public void Enqueue_QueueFull_DropsTheOldestAndKeepsTheNewest()
    {
        var queue = NewQueue(capacity: 2);

        foreach (var minute in Enumerable.Range(0, 5))
        {
            queue.Enqueue(TestCandles.At(minute));
        }

        Assert.Equal([TestCandles.At(3), TestCandles.At(4)], Drain(queue));
    }

    [Fact]
    public void Enqueue_DefaultCapacity_HoldsADayOfCandlesForTwentySymbols()
    {
        var queue = new CandleQueue(Options.Create(new IngestionOptions()), NullLogger<CandleQueue>.Instance);
        var candles = Enumerable.Range(0, 24 * 60)
            .SelectMany(minute => Enumerable.Range(0, 20).Select(symbol => TestCandles.At(minute, $"SYM{symbol:D2}")))
            .ToList();

        foreach (var candle in candles)
        {
            queue.Enqueue(candle);
        }

        Assert.Equal(candles, Drain(queue));
    }

    private static CandleQueue NewQueue(int capacity) =>
        new(Options.Create(new IngestionOptions { QueueCapacity = capacity }), NullLogger<CandleQueue>.Instance);

    private static List<Candle> Drain(CandleQueue queue)
    {
        var candles = new List<Candle>();
        while (queue.TryRead(out var candle))
        {
            candles.Add(candle);
        }

        return candles;
    }
}
