using System.ComponentModel.DataAnnotations;

namespace MarketData.Application.Ingestion;

public sealed class IngestionOptions
{
    public const string SectionName = "Ingestion";

    /// <summary>
    /// Candles waiting to be saved. The default holds 24 hours for the default cap of 20 Symbols (20 x 1,440 = 28,800),
    /// so a database outage of up to a day loses nothing while the service keeps running. Raise it with the cap.
    /// When full the oldest Candle is dropped so the feed never waits for the database.
    /// </summary>
    [Range(1, 1_000_000)]
    public int QueueCapacity { get; set; } = 20 * 24 * 60;

    [Range(1, 10_000)]
    public int MaxBatchSize { get; set; } = 500;

    [Range(typeof(TimeSpan), "00:00:00", "00:10:00")]
    public TimeSpan WriterRetryDelay { get; set; } = TimeSpan.FromSeconds(5);
}
