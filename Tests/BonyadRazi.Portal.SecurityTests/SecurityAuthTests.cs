using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace BonyadRazi.Portal.SecurityTests;

public sealed class SecurityAuthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SecurityAuthTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Companies_Get_WithoutToken_ShouldBe401()
    {
        var client = _factory.CreateClient();

        var company = Guid.NewGuid();
        var resp = await client.GetAsync($"/api/companies/{company}");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Companies_Get_CrossTenant_ShouldBe403()
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
            roles: new[] { "Admin" },
            issuer: issuer,
            audience: audience);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync($"/api/companies/{routeTenant}");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Companies_Get_SameTenant_ShouldBe200()
    {
        var client = _factory.CreateClient();

        var cfg = _factory.Services.GetRequiredService<IConfiguration>();
        var issuer = cfg["Jwt:Issuer"]!;
        var audience = cfg["Jwt:Audience"]!;

        var tenant = Guid.NewGuid();

        var token = JwtTestToken.Create(
            userId: Guid.NewGuid(),
            companyCode: tenant,
            roles: new[] { "Admin" },
            issuer: issuer,
            audience: audience);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync($"/api/companies/{tenant}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}