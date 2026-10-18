using System.ComponentModel.DataAnnotations;

namespace MarketData.Application.Ingestion;

public sealed class IngestionOptions
{
    public const string SectionName = "Ingestion";

    /// <summary>Candles waiting to be saved; when full the oldest is dropped so the feed never waits for the database.</summary>
    [Range(1, 1_000_000)]
    public int QueueCapacity { get; set; } = 10_000;

    [Range(1, 10_000)]
    public int MaxBatchSize { get; set; } = 500;

    [Range(typeof(TimeSpan), "00:00:00", "00:10:00")]
    public TimeSpan WriterRetryDelay { get; set; } = TimeSpan.FromSeconds(5);
}
