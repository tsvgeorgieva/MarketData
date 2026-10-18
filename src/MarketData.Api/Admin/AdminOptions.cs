using System.ComponentModel.DataAnnotations;

namespace MarketData.Api.Admin;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    [Required, MinLength(16)]
    public string ApiKey { get; set; } = "";
}
