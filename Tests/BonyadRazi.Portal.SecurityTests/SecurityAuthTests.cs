using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace BonyadRazi.Portal.SecurityTests;

public class SecurityAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SecurityAuthTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
        });
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

        // توکن برای یک tenant
        var tokenTenant = Guid.NewGuid();

        // مسیر با tenant متفاوت => باید Forbid شود (403)
        var routeTenant = Guid.NewGuid();

        var token = JwtTestToken.Create(
            userId: Guid.NewGuid(),
            companyCode: tokenTenant,
            roles: new[] { "Admin" },
            issuer: issuer,
            audience: audience);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

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

        // tenant یکسان در توکن و مسیر => باید OK شود (200)
        var tenant = Guid.NewGuid();

        var token = JwtTestToken.Create(
            userId: Guid.NewGuid(),
            companyCode: tenant,
            roles: new[] { "Admin" },
            issuer: issuer,
            audience: audience);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync($"/api/companies/{tenant}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}