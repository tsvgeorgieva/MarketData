using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using MarketData.Domain.Candles;
using Microsoft.Extensions.Options;

namespace MarketData.Application.Ingestion;

/// <summary>
/// Holds closed Candles between the Binance feed and the database writer, so a slow database never makes the feed wait.
/// Its default size covers a 24-hour database outage; beyond that the oldest Candle is dropped and a later Backfill restores it.
/// </summary>
public sealed class CandleIngestionQueue
{
    private readonly Channel<Candle> _candles;

    public CandleIngestionQueue(IOptions<IngestionOptions> options, ILogger<CandleIngestionQueue> logger)
    {
        _candles = Channel.CreateBounded<Candle>(
            new BoundedChannelOptions(options.Value.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
            },
            dropped => logger.LogWarning("Queue full; dropped the {Symbol} candle of {OpenTime}", dropped.Symbol, dropped.OpenTime));
    }

    public void Enqueue(Candle candle) => _candles.Writer.TryWrite(candle);

    public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken) =>
        _candles.Reader.WaitToReadAsync(cancellationToken);

    public bool TryRead([MaybeNullWhen(false)] out Candle candle) => _candles.Reader.TryRead(out candle);
}
