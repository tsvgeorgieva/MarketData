using MarketData.Application.Ingestion;
using MarketData.Domain.Candles;

namespace MarketData.UnitTests.Ingestion;

/// <summary>Keeps Candles in memory; can be held back until released or made to fail a number of times.</summary>
internal sealed class FakeCandleStore : ICandleStore
{
    private readonly List<Candle> _stored = [];
    private readonly Lock _lock = new();

    /// <summary>While set, saving waits until the task completes.</summary>
    public Task? HoldUntil { get; set; }

    public int FailuresToThrow { get; set; }

    public IReadOnlyList<Candle> Stored
    {
        get
        {
            lock (_lock)
            {
                return [.. _stored];
            }
        }
    }

    public async Task<int> InsertMissingAsync(IReadOnlyCollection<Candle> candles, CancellationToken cancellationToken)
    {
        if (HoldUntil is not null)
        {
            await HoldUntil;
        }

        if (FailuresToThrow > 0)
        {
            FailuresToThrow--;
            throw new InvalidOperationException("The database is down.");
        }

        lock (_lock)
        {
            _stored.AddRange(candles);
        }

        return candles.Count;
    }
}
