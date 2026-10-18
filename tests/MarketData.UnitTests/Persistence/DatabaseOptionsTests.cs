using System.ComponentModel.DataAnnotations;
using MarketData.Infrastructure.Persistence;

namespace MarketData.UnitTests.Persistence;

public class DatabaseOptionsTests
{
    [Fact]
    public void Validate_EmptyConnectionString_IsInvalid()
    {
        var options = new DatabaseOptions { ConnectionString = "" };

        Assert.False(IsValid(options));
    }

    [Fact]
    public void Validate_ConnectionStringPresent_IsValid()
    {
        var options = new DatabaseOptions { ConnectionString = "Server=localhost" };

        Assert.True(IsValid(options));
    }

    private static bool IsValid(DatabaseOptions options) =>
        Validator.TryValidateObject(options, new ValidationContext(options), [], validateAllProperties: true);
}
