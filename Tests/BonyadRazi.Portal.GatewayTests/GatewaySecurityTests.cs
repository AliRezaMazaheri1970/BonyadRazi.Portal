using BonyadRazi.Portal.SecurityTests;
using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace BonyadRazi.Portal.GatewayTests;

public class GatewaySecurityTests
{
    [Fact]
    public async Task GatewayHealth_IsAlwaysPublic_200()
    {
        await using var factory = new GatewayFactory(new()
        {
            ["Security:AllowedCidrs:0"] = null, // allow all
        });

        var client = factory.CreateClient();
        var res = await client.GetAsync("/gateway/health");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task PublicAuthEndpoints_WithoutJwt_AreAllowed_200()
    {
        await using var factory = new GatewayFactory(new()
        {
            ["Security:AllowedCidrs:0"] = null, // allow all
        });

        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/auth/login", new StringContent("{}"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/auth/refresh", new StringContent("{}"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/auth/revoke", new StringContent("{}"))).StatusCode);
    }

    [Fact]
    public async Task ProtectedApi_WithoutJwt_Returns401()
    {
        await using var factory = new GatewayFactory(new()
        {
            ["Security:AllowedCidrs:0"] = null, // allow all
            ["Security:ValidateJwtAtGateway"] = "true",
            ["Jwt:Issuer"] = "BonyadRazi.Auth",
            ["Jwt:Audience"] = "BonyadRazi.Portal",
        });

        var client = factory.CreateClient();
        var res = await client.GetAsync("/api/companies/anything");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task ProtectedApi_WithValidJwt_Returns200()
    {
        await using var factory = new GatewayFactory(new()
        {
            ["Security:AllowedCidrs:0"] = null, // allow all
            ["Security:ValidateJwtAtGateway"] = "true",
            ["Jwt:Issuer"] = "BonyadRazi.Auth",
            ["Jwt:Audience"] = "BonyadRazi.Portal",
        });

        var client = factory.CreateClient();

        var token = JwtTestToken.Create(
            userId: Guid.NewGuid(),
            companyCode: Guid.Parse("89467084-2A33-4054-8418-97E5E59ED17F"),
            roles: new[] { "Admin" },
            issuer: "BonyadRazi.Auth",
            audience: "BonyadRazi.Portal"
        );

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/companies/demo");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task IpAllowlist_WhenNoMatch_Returns403()
    {
        await using var factory = new GatewayFactory(new()
        {
            ["Security:BypassIpAllowlistInTesting"] = "false",
            ["Security:AllowLoopbackInDevelopment"] = "false",
            ["Security:AllowedCidrs:0"] = "192.168.93.0/27",
        });

        var client = factory.CreateClient();

        // login endpoint is POST
        var res = await client.PostAsync("/api/auth/login", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}