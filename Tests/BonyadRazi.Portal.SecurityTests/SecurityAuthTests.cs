using BonyadRazi.Portal.Api.Audit;
using BonyadRazi.Portal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
    public async Task Companies_Get_CrossTenant_ShouldWriteTenantViolationAuditLog()
    {
        var client = _factory.CreateClient();

        var cfg = _factory.Services.GetRequiredService<IConfiguration>();
        var issuer = cfg["Jwt:Issuer"]!;
        var audience = cfg["Jwt:Audience"]!;

        var userId = Guid.NewGuid();
        var tokenTenant = Guid.NewGuid();
        var routeTenant = Guid.NewGuid();

        var token = JwtTestToken.Create(
            userId: userId,
            companyCode: tokenTenant,
            roles: new[] { "Admin" },
            issuer: issuer,
            audience: audience);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync($"/api/companies/{routeTenant}");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RasfPortalDbContext>();

        var auditLog = await db.UserActionLogs
            .OrderByDescending(x => x.Utc)
            .FirstOrDefaultAsync(x =>
                x.ActionType == AuditActionTypes.SecurityTenantViolation &&
                x.StatusCode == 403 &&
                x.Path == $"/api/companies/{routeTenant}" &&
                x.Reason == "TENANT_MISMATCH");

        Assert.NotNull(auditLog);
        Assert.Equal(userId, auditLog!.UserId);
        Assert.Equal("GET", auditLog.Method);
        Assert.False(string.IsNullOrWhiteSpace(auditLog.TraceId));
        Assert.False(string.IsNullOrWhiteSpace(auditLog.RemoteIp));
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

    [Theory]
    [InlineData("pwd")]
    [InlineData("jwt")]
    [InlineData("apikey")]
    [InlineData("connection_string")]
    [InlineData("client_secret")]
    [InlineData("currentPassword")]
    [InlineData("newPassword")]
    public async Task Unauthorized_Request_WithSensitiveQueryKey_ShouldRedactQueryStringInAuditLog(string sensitiveKey)
    {
        var client = _factory.CreateClient();

        var marker = Guid.NewGuid().ToString("N");
        var path = $"/api/companies/{Guid.NewGuid()}";
        var query = $"?marker={marker}&{sensitiveKey}=super-secret-value";

        var resp = await client.GetAsync(path + query);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RasfPortalDbContext>();

        var auditLog = await db.UserActionLogs
            .OrderByDescending(x => x.Utc)
            .FirstOrDefaultAsync(x =>
                x.ActionType == AuditActionTypes.SecurityAccessDenied &&
                x.StatusCode == 401 &&
                x.Path == path &&
                x.Reason == "UNAUTHORIZED_401");

        Assert.NotNull(auditLog);

        var metadata = auditLog!.MetadataJson ?? string.Empty;

        Assert.Contains(AuditRedaction.RedactedValue, metadata);
        Assert.DoesNotContain("super-secret-value", metadata);
        Assert.DoesNotContain(sensitiveKey, metadata, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApiHealth_WithoutToken_ShouldBe401()
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task ApiHealth_WithNonAdminToken_ShouldBe403()
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

        var resp = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task ApiHealth_WithAdminToken_ShouldBe200()
    {
        var client = _factory.CreateClient();

        var cfg = _factory.Services.GetRequiredService<IConfiguration>();
        var issuer = cfg["Jwt:Issuer"]!;
        var audience = cfg["Jwt:Audience"]!;

        var token = JwtTestToken.Create(
            userId: Guid.NewGuid(),
            companyCode: Guid.NewGuid(),
            roles: new[] { "Admin" },
            issuer: issuer,
            audience: audience);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();

        Assert.Contains("\"status\":\"ok\"", body);
        Assert.Contains("\"where\":\"api\"", body);
    }

    [Fact]
    public async Task ApiHealth_WithoutToken_ShouldWriteAccessDeniedAuditLog()
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RasfPortalDbContext>();

        var auditLog = await db.UserActionLogs
            .OrderByDescending(x => x.Utc)
            .FirstOrDefaultAsync(x =>
                x.ActionType == AuditActionTypes.SecurityAccessDenied &&
                x.StatusCode == 401 &&
                x.Path == "/health" &&
                x.Reason == "UNAUTHORIZED_401");

        Assert.NotNull(auditLog);
        Assert.Equal("GET", auditLog!.Method);
        Assert.False(string.IsNullOrWhiteSpace(auditLog.TraceId));
        Assert.False(string.IsNullOrWhiteSpace(auditLog.RemoteIp));
    }


}