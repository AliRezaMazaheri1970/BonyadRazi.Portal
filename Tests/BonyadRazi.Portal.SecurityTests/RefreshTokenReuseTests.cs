using BonyadRazi.Portal.Api.Audit;
using BonyadRazi.Portal.Infrastructure.Audit.Entities;
using BonyadRazi.Portal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Xunit;

namespace BonyadRazi.Portal.SecurityTests;

public sealed class RefreshTokenReuseTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public RefreshTokenReuseTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Refresh_ReusingRotatedRefreshToken_ShouldRevokeActiveTokensForUser()
    {
        var client = _factory.CreateClient();

        var userId = Guid.NewGuid();
        var companyCode = Guid.NewGuid();
        var username = $"reuse-{Guid.NewGuid():N}";
        const string password = "P@ssw0rd!";

        await SeedUser(userId, companyCode, username, password);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Username = username,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginTokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(loginTokens);
        Assert.False(string.IsNullOrWhiteSpace(loginTokens!.refresh_token));

        var firstRefreshToken = loginTokens.refresh_token;

        var firstRefreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refresh_token = firstRefreshToken
        });

        Assert.Equal(HttpStatusCode.OK, firstRefreshResponse.StatusCode);

        var rotatedTokens = await firstRefreshResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(rotatedTokens);
        Assert.False(string.IsNullOrWhiteSpace(rotatedTokens!.refresh_token));
        Assert.NotEqual(firstRefreshToken, rotatedTokens.refresh_token);

        var reuseResponse = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refresh_token = firstRefreshToken
        });

        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RasfPortalDbContext>();

        var activeTokenCount = await db.RefreshTokens
            .Where(x =>
                x.UserAccountId == userId &&
                x.RevokedUtc == null &&
                x.ExpiresUtc > DateTime.UtcNow)
            .CountAsync();

        Assert.Equal(0, activeTokenCount);

        var reuseDetectedCount = await db.RefreshTokens
            .Where(x =>
                x.UserAccountId == userId &&
                x.RevokedUtc != null &&
                x.RevokeReason == "reuse_detected")
            .CountAsync();

        Assert.True(reuseDetectedCount >= 1);
    }

    [Fact]
    public async Task Refresh_ReusingRotatedRefreshToken_ShouldWriteAuditLog()
    {
        var client = _factory.CreateClient();

        var userId = Guid.NewGuid();
        var companyCode = Guid.NewGuid();
        var username = $"reuse-audit-{Guid.NewGuid():N}";
        const string password = "P@ssw0rd!";

        await SeedUser(userId, companyCode, username, password);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Username = username,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginTokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(loginTokens);
        Assert.False(string.IsNullOrWhiteSpace(loginTokens!.refresh_token));

        var firstRefreshToken = loginTokens.refresh_token;

        var firstRefreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refresh_token = firstRefreshToken
        });

        Assert.Equal(HttpStatusCode.OK, firstRefreshResponse.StatusCode);

        var reuseResponse = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refresh_token = firstRefreshToken
        });

        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RasfPortalDbContext>();

        var auditLog = await db.UserActionLogs
            .OrderByDescending(x => x.Utc)
            .FirstOrDefaultAsync(x =>
                x.ActionType == AuditActionTypes.AuthRefreshReuseDetected &&
                x.StatusCode == 401 &&
                x.Path == "/api/auth/refresh" &&
                x.Reason == "REFRESH_TOKEN_REUSE_DETECTED");

        Assert.NotNull(auditLog);
        Assert.Equal(userId, auditLog!.UserId);
        Assert.Equal("POST", auditLog.Method);
        Assert.False(string.IsNullOrWhiteSpace(auditLog.TraceId));
        Assert.False(string.IsNullOrWhiteSpace(auditLog.RemoteIp));
    }

    private async Task SeedUser(
        Guid userId,
        Guid companyCode,
        string username,
        string password)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RasfPortalDbContext>();

        var salt = RandomNumberGenerator.GetBytes(16);
        const int iterations = 100_000;

        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password: password,
            salt: salt,
            iterations: iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 32);

        db.UserAccounts.Add(new UserAccount
        {
            Id = userId,
            Username = username,
            PasswordHash = hash,
            PasswordSalt = salt,
            PasswordIterations = iterations,
            Roles = "User",
            CompanyCode = companyCode,
            IsActive = true,
            FailedLoginCount = 0,
            LockoutEndUtc = null
        });

        await db.SaveChangesAsync();
    }

    private sealed record TokenResponse(
        string access_token,
        int expires_in,
        string refresh_token,
        int refresh_expires_in);
}