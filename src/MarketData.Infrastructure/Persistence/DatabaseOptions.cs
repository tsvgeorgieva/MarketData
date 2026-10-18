using System.ComponentModel.DataAnnotations;

namespace MarketData.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public const string ConnectionStringName = "MarketData";

    [Required]
    public string ConnectionString { get; set; } = "";

    public bool MigrateOnStartup { get; set; }
}
