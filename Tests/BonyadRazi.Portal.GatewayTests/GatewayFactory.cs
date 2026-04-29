using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using GatewayEntryPoint = BonyadRazi.Portal.Gateway.GatewayEntryPoint;

namespace BonyadRazi.Portal.GatewayTests;

public sealed class GatewayFactory : WebApplicationFactory<GatewayEntryPoint>
{
    private readonly Dictionary<string, string?> _overrides;

    public GatewayFactory(Dictionary<string, string?>? overrides = null)
    {
        _overrides = new Dictionary<string, string?>
        {
            // Test environment must not be blocked by production IP allowlist.
            // The dedicated IP allowlist test can override this to false.
            ["Security:BypassIpAllowlistInTesting"] = "true",

            // Keep gateway JWT validation enabled by default in tests.
            ["Security:ValidateJwtAtGateway"] = "true",

            // Force test endpoints instead of real YARP upstream calls.
            ["Security:ForceTestingEndpoints"] = "true",

            // Deterministic JWT settings for test tokens.
            ["Jwt:Issuer"] = "BonyadRazi.Auth",
            ["Jwt:Audience"] = "BonyadRazi.Portal",

            // Deterministic allow prefixes for tests.
            // Deterministic allow prefixes for tests.
            ["Security:ApiAllowPrefixes:0"] = "/api/auth",
            ["Security:ApiAllowPrefixes:1"] = "/api/users",
            ["Security:ApiAllowPrefixes:2"] = "/api/companies",
            ["Security:ApiAllowPrefixes:3"] = "/api/audit",
            ["Security:ApiAllowPrefixes:4"] = "/api/diagnostics",
            ["Security:ApiAllowPrefixes:5"] = "/health",
            ["Security:ApiAllowPrefixes:6"] = "/gateway/health",
        };

        if (overrides is not null)
        {
            foreach (var item in overrides)
            {
                _overrides[item.Key] = item.Value;
            }
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(_overrides);
        });
    }
}