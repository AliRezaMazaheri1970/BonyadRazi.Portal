using BonyadRazi.Portal.Api.Audit;
using BonyadRazi.Portal.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
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

        var token = JwtTestToken.Create(
            userId: Guid.NewGuid(),
            companyCode: Guid.NewGuid(),
            roles: new[] { "User" },
            issuer: issuer,
            audience: audience);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

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
            roles: new[] { "Admin" },
            issuer: issuer,
            audience: audience);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/audit/denied?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task AuditDenied_Get_WithAuditorToken_ShouldNotLeakOtherTenantLogs()
    {
        var client = _factory.CreateClient();

        var cfg = _factory.Services.GetRequiredService<IConfiguration>();
        var issuer = cfg["Jwt:Issuer"]!;
        var audience = cfg["Jwt:Audience"]!;

        var auditorCompany = Guid.NewGuid();
        var otherCompany = Guid.NewGuid();

        var ownTenantMarker = $"/security-test/audit-denied/{Guid.NewGuid()}/own";
        var otherTenantMarker = $"/security-test/audit-denied/{Guid.NewGuid()}/other";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RasfPortalDbContext>();

            db.UserActionLogs.Add(new()
            {
                Utc = DateTime.UtcNow.AddMinutes(-2),
                UserId = Guid.NewGuid(),
                CompanyCode = auditorCompany,
                ActionType = AuditActionTypes.SecurityAccessDenied,
                StatusCode = 403,
                Reason = "FORBIDDEN_403",
                Method = "GET",
                Path = ownTenantMarker,
                RemoteIp = "127.0.0.1",
                UserAgent = "security-test",
                TraceId = Guid.NewGuid().ToString("N")
            });

            db.UserActionLogs.Add(new()
            {
                Utc = DateTime.UtcNow.AddMinutes(-1),
                UserId = Guid.NewGuid(),
                CompanyCode = otherCompany,
                ActionType = AuditActionTypes.SecurityAccessDenied,
                StatusCode = 403,
                Reason = "FORBIDDEN_403",
                Method = "GET",
                Path = otherTenantMarker,
                RemoteIp = "127.0.0.1",
                UserAgent = "security-test",
                TraceId = Guid.NewGuid().ToString("N")
            });

            await db.SaveChangesAsync();
        }

        var token = JwtTestToken.Create(
            userId: Guid.NewGuid(),
            companyCode: auditorCompany,
            roles: new[] { "Auditor" },
            issuer: issuer,
            audience: audience);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // The attacker asks for another tenant.
        // The controller must ignore this and force companyCode to the claim tenant.
        var resp = await client.GetAsync(
            $"/api/audit/denied?companyCode={otherCompany}&statusCode=403&page=1&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();

        Assert.Contains(ownTenantMarker, body);
        Assert.DoesNotContain(otherTenantMarker, body);
        Assert.Contains(auditorCompany.ToString(), body);
        Assert.DoesNotContain(otherCompany.ToString(), body);
    }

    [Fact]
    public async Task AuditDenied_Get_WithSuperAdminToken_ShouldAllowCrossTenantFilter()
    {
        var client = _factory.CreateClient();

        var cfg = _factory.Services.GetRequiredService<IConfiguration>();
        var issuer = cfg["Jwt:Issuer"]!;
        var audience = cfg["Jwt:Audience"]!;

        var superAdminCompany = Guid.NewGuid();
        var targetCompany = Guid.NewGuid();

        var targetMarker = $"/security-test/audit-denied/{Guid.NewGuid()}/superadmin-target";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RasfPortalDbContext>();

            db.UserActionLogs.Add(new()
            {
                Utc = DateTime.UtcNow,
                UserId = Guid.NewGuid(),
                CompanyCode = targetCompany,
                ActionType = AuditActionTypes.SecurityAccessDenied,
                StatusCode = 403,
                Reason = "FORBIDDEN_403",
                Method = "GET",
                Path = targetMarker,
                RemoteIp = "127.0.0.1",
                UserAgent = "security-test",
                TraceId = Guid.NewGuid().ToString("N")
            });

            await db.SaveChangesAsync();
        }

        var token = JwtTestToken.Create(
            userId: Guid.NewGuid(),
            companyCode: superAdminCompany,
            roles: new[] { "SuperAdmin" },
            issuer: issuer,
            audience: audience);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync(
            $"/api/audit/denied?companyCode={targetCompany}&statusCode=403&page=1&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();

        Assert.Contains(targetMarker, body);
        Assert.Contains(targetCompany.ToString(), body);
    }

    [Fact]
    public async Task AuditDenied_Get_ShouldIncludeTenantViolationEvents()
    {
        var client = _factory.CreateClient();

        var cfg = _factory.Services.GetRequiredService<IConfiguration>();
        var issuer = cfg["Jwt:Issuer"]!;
        var audience = cfg["Jwt:Audience"]!;

        var company = Guid.NewGuid();
        var marker = $"/security-test/audit-denied/{Guid.NewGuid()}/tenant-violation";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RasfPortalDbContext>();

            db.UserActionLogs.Add(new()
            {
                Utc = DateTime.UtcNow,
                UserId = Guid.NewGuid(),
                CompanyCode = company,
                ActionType = AuditActionTypes.SecurityTenantViolation,
                StatusCode = 403,
                Reason = "TENANT_MISMATCH",
                Method = "GET",
                Path = marker,
                RemoteIp = "127.0.0.1",
                UserAgent = "security-test",
                TraceId = Guid.NewGuid().ToString("N")
            });

            await db.SaveChangesAsync();
        }

        var token = JwtTestToken.Create(
            userId: Guid.NewGuid(),
            companyCode: company,
            roles: new[] { "Auditor" },
            issuer: issuer,
            audience: audience);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/audit/denied?page=1&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();

        Assert.Contains(marker, body);
        Assert.Contains(AuditActionTypes.SecurityTenantViolation, body);
    }
}