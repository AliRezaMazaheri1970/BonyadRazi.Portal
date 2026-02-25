using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace BonyadRazi.Portal.SecurityTests;

public sealed class AuditDeniedSecurityTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuditDeniedSecurityTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuditDenied_Get_WithoutToken_ShouldBe401()
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync("/api/audit/denied?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task AuditDenied_Get_WithNonAdminToken_ShouldBe403()
    {
        var client = _factory.CreateClient();

        var cfg = _factory.Services.GetRequiredService<IConfiguration>();
        var issuer = cfg["Jwt:Issuer"]!;
        var audience = cfg["Jwt:Audience"]!;

        // اینجا tenant مهم نیست، چون endpoint admin-only است
        var token = JwtTestToken.Create(
            userId: Guid.NewGuid(),
            companyCode: Guid.NewGuid(),
            roles: new[] { "User" }, // Non-admin
            issuer: issuer,
            audience: audience);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/audit/denied?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task AuditDenied_Get_WithAdminToken_ShouldBe200()
    {
        var client = _factory.CreateClient();

        var cfg = _factory.Services.GetRequiredService<IConfiguration>();
        var issuer = cfg["Jwt:Issuer"]!;
        var audience = cfg["Jwt:Audience"]!;

        var token = JwtTestToken.Create(
            userId: Guid.NewGuid(),
            companyCode: Guid.NewGuid(),
            roles: new[] { "Admin" }, // Admin
            issuer: issuer,
            audience: audience);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/audit/denied?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}