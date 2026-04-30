using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace BonyadRazi.Portal.SecurityTests;

public sealed class DiagnosticsSecurityTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DiagnosticsSecurityTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Diagnostics_AuthTest_WithoutToken_ShouldReturn401()
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync("/api/diagnostics/auth-test");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Diagnostics_AuthTest_WithValidToken_ShouldReturn200()
    {
        var client = _factory.CreateClient();

        var cfg = _factory.Services.GetRequiredService<IConfiguration>();
        var issuer = cfg["Jwt:Issuer"]!;
        var audience = cfg["Jwt:Audience"]!;

        var token = JwtTestToken.Create(
            userId: Guid.NewGuid(),
            companyCode: Guid.NewGuid(),
            roles: new[] { "User" },
            issuer: issuer,
            audience: audience);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/diagnostics/auth-test");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Diagnostics_TenantTest_SameTenant_WithUserRole_ShouldReturn200()
    {
        var client = _factory.CreateClient();

        var cfg = _factory.Services.GetRequiredService<IConfiguration>();
        var issuer = cfg["Jwt:Issuer"]!;
        var audience = cfg["Jwt:Audience"]!;

        var tenant = Guid.NewGuid();

        var token = JwtTestToken.Create(
            userId: Guid.NewGuid(),
            companyCode: tenant,
            roles: new[] { "User" },
            issuer: issuer,
            audience: audience);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync($"/api/diagnostics/tenant-test/{tenant}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Diagnostics_TenantTest_CrossTenant_WithUserRole_ShouldReturn403()
    {
        var client = _factory.CreateClient();

        var cfg = _factory.Services.GetRequiredService<IConfiguration>();
        var issuer = cfg["Jwt:Issuer"]!;
        var audience = cfg["Jwt:Audience"]!;

        var tokenTenant = Guid.NewGuid();
        var routeTenant = Guid.NewGuid();

        var token = JwtTestToken.Create(
            userId: Guid.NewGuid(),
            companyCode: tokenTenant,
            roles: new[] { "User" },
            issuer: issuer,
            audience: audience);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync($"/api/diagnostics/tenant-test/{routeTenant}");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Health_WithoutToken_ShouldReturn401()
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}