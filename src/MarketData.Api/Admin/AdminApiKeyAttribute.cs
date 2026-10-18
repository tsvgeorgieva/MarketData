using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace MarketData.Api.Admin;

/// <summary>Allows the request only when its <c>X-Api-Key</c> header holds the configured admin key; otherwise 401.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdminApiKeyAttribute : Attribute, IAuthorizationFilter
{
    public const string HeaderName = "X-Api-Key";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var expected = context.HttpContext.RequestServices.GetRequiredService<IOptions<AdminOptions>>().Value.ApiKey;
        var supplied = context.HttpContext.Request.Headers[HeaderName].ToString();

        // Hashing first gives both sides the same length, so the comparison takes the same time for any wrong key.
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(supplied)), SHA256.HashData(Encoding.UTF8.GetBytes(expected))))
        {
            context.Result = new UnauthorizedResult();
        }
    }
}
