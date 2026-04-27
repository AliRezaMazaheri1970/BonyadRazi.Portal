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
        _overrides = overrides ?? new Dictionary<string, string?>();
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