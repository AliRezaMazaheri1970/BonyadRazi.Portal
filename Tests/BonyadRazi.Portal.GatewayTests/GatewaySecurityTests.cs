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
            ["Security:AllowedCidrs:0"] = null
        });

        var client = factory.CreateClient();

        var response = await client.GetAsync("/gateway/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PublicAuthEndpoints_WithoutJwt_AreAllowed_200()
    {
        await using var factory = new GatewayFactory(new()
        {
            ["Security:AllowedCidrs:0"] = null,
            ["Security:ValidateJwtAtGateway"] = "true",
            ["Jwt:Issuer"] = "BonyadRazi.Auth",
            ["Jwt:Audience"] = "BonyadRazi.Portal"
        });

        var client = factory.CreateClient();

        var loginResponse = await client.PostAsync("/api/auth/login", new StringContent("{}"));
        var refreshResponse = await client.PostAsync("/api/auth/refresh", new StringContent("{}"));
        var revokeResponse = await client.PostAsync("/api/auth/revoke", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
    }

    [Fact]
    public async Task ProtectedApi_WithoutJwt_Returns401()
    {
        await using var factory = new GatewayFactory(new()
        {
            ["Security:AllowedCidrs:0"] = null,
            ["Security:ValidateJwtAtGateway"] = "true",
            ["Jwt:Issuer"] = "BonyadRazi.Auth",
            ["Jwt:Audience"] = "BonyadRazi.Portal"
        });

        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/companies/anything");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedApi_WithValidJwt_Returns200()
    {
        await using var factory = new GatewayFactory(new()
        {
            ["Security:AllowedCidrs:0"] = null,
            ["Security:ValidateJwtAtGateway"] = "true",
            ["Jwt:Issuer"] = "BonyadRazi.Auth",
            ["Jwt:Audience"] = "BonyadRazi.Portal"
        });

        var client = factory.CreateClient();

        var token = JwtTestToken.Create(
            userId: Guid.NewGuid(),
            companyCode: Guid.Parse("89467084-2A33-4054-8418-97E5E59ED17F"),
            roles: new[] { "Admin" },
            issuer: "BonyadRazi.Auth",
            audience: "BonyadRazi.Portal");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/companies/demo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task IpAllowlist_WhenNoMatch_Returns403()
    {
        await using var factory = new GatewayFactory(new()
        {
            ["Security:BypassIpAllowlistInTesting"] = "false",
            ["Security:AllowLoopbackInDevelopment"] = "false",
            ["Security:ValidateJwtAtGateway"] = "false",
            ["Security:AllowedCidrs:0"] = "192.168.93.0/27",
            ["Jwt:Issuer"] = "BonyadRazi.Auth",
            ["Jwt:Audience"] = "BonyadRazi.Portal"
        });

        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/auth/login", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnknownApiPath_Returns404()
    {
        await using var factory = new GatewayFactory(new()
        {
            ["Security:AllowedCidrs:0"] = null,
            ["Security:ValidateJwtAtGateway"] = "false",
            ["Security:ApiAllowPrefixes:0"] = "/api/auth",
            ["Security:ApiAllowPrefixes:1"] = "/api/users",
            ["Security:ApiAllowPrefixes:2"] = "/api/companies",
            ["Security:ApiAllowPrefixes:3"] = "/api/audit",
            ["Jwt:Issuer"] = "BonyadRazi.Auth",
            ["Jwt:Audience"] = "BonyadRazi.Portal"
        });


        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/unknown/test");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}