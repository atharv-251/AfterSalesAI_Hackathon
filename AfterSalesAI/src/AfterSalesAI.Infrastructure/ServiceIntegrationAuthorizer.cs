using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace AfterSalesAI.Infrastructure;

public sealed class ServiceIntegrationOptions
{
    public const string HeaderName = "X-AfterSales-Integration-Key";

    public string ApiKey { get; set; } = string.Empty;
}

public sealed class ServiceIntegrationAuthorizer(IOptions<ServiceIntegrationOptions> options)
{
    public bool IsAuthorized(string? supplied)
    {
        var expected = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(expected)
            || string.IsNullOrWhiteSpace(supplied)) return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied));
    }
}
