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

public sealed class UsernameLoginRateLimitTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public UsernameLoginRateLimitTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_SameUsernameTooManyAttempts_ShouldReturn429()
    {
        var client = _factory.CreateClient();

        var userId = Guid.NewGuid();
        var companyCode = Guid.NewGuid();
        var username = $"ratelimit-{Guid.NewGuid():N}";
        const string correctPassword = "P@ssw0rd!";
        const string wrongPassword = "WrongP@ssw0rd!";

        await SeedUser(userId, companyCode, username, correctPassword);

        HttpResponseMessage? lastResponse = null;

        for (var i = 0; i < 21; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/auth/login", new
            {
                Username = username,
                Password = wrongPassword
            });
        }

        Assert.NotNull(lastResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
        Assert.True(lastResponse.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task Login_SameUsernameTooManyAttempts_ShouldWriteAuditLog()
    {
        var client = _factory.CreateClient();

        var userId = Guid.NewGuid();
        var companyCode = Guid.NewGuid();
        var username = $"audit-ratelimit-{Guid.NewGuid():N}";
        const string correctPassword = "P@ssw0rd!";
        const string wrongPassword = "WrongP@ssw0rd!";

        await SeedUser(userId, companyCode, username, correctPassword);

        HttpResponseMessage? lastResponse = null;

        for (var i = 0; i < 21; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/auth/login", new
            {
                Username = username,
                Password = wrongPassword
            });
        }

        Assert.NotNull(lastResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RasfPortalDbContext>();

        var auditLog = await db.UserActionLogs
            .OrderByDescending(x => x.Utc)
            .FirstOrDefaultAsync(x =>
                x.ActionType == AuditActionTypes.SecurityLoginRateLimited &&
                x.StatusCode == 429 &&
                x.Path == "/api/auth/login" &&
                x.Reason == "LOGIN_RATE_LIMITED_BY_USERNAME");

        Assert.NotNull(auditLog);
        Assert.Equal("POST", auditLog!.Method);
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

        var existing = await db.UserAccounts.SingleOrDefaultAsync(x => x.Username == username);
        if (existing is not null)
        {
            db.UserAccounts.Remove(existing);
            await db.SaveChangesAsync();
        }

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
}