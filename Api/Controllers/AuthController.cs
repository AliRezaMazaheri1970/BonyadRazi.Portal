using BonyadRazi.Portal.Api.Security;
using BonyadRazi.Portal.Infrastructure.Audit.Entities;
using BonyadRazi.Portal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BonyadRazi.Portal.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly RasfPortalDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly Pbkdf2PasswordHasher _hasher;
    private readonly IUsernameLoginRateLimiter _usernameLoginRateLimiter;

    // Lockout policy (Dev)
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    public AuthController(
        RasfPortalDbContext db,
        IConfiguration cfg,
        Pbkdf2PasswordHasher hasher,
        IUsernameLoginRateLimiter usernameLoginRateLimiter)
    {
        _db = db;
        _cfg = cfg;
        _hasher = hasher;
        _usernameLoginRateLimiter = usernameLoginRateLimiter;
    }

    public sealed record LoginRequest(string Username, string Password);
    public sealed record RefreshRequest(string refresh_token);
    public sealed record RevokeRequest(string refresh_token);

    public sealed record TokenResponse(
        string access_token,
        int expires_in,
        string refresh_token,
        int refresh_expires_in
    );

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> Login([FromBody] LoginRequest req)
    {
        var username = (req.Username ?? "").Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(req.Password))
            return Unauthorized(new { message = "Invalid credentials." });

        var now = DateTime.UtcNow;

        var usernameLimit = _usernameLoginRateLimiter.Check(username, now);
        if (!usernameLimit.Allowed)
        {
            Response.Headers.RetryAfter = usernameLimit.RetryAfterSeconds.ToString();

            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                message = "Too many login attempts. Try later."
            });
        }

        var user = await _db.UserAccounts.SingleOrDefaultAsync(x => x.Username == username);

        // جلوگیری از user-enumeration
        if (user is null || !user.IsActive)
            return Unauthorized(new { message = "Invalid credentials." });

        // Lockout check
        if (user.LockoutEndUtc is not null && user.LockoutEndUtc.Value > now)
            return Unauthorized(new { message = "Account locked. Try later." });

        // Verify password (PBKDF2)
        var ok = _hasher.Verify(req.Password, user.PasswordSalt, user.PasswordIterations, user.PasswordHash);
        if (!ok)
        {
            user.FailedLoginCount++;

            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockoutEndUtc = now.Add(LockoutDuration);
                user.FailedLoginCount = 0;
            }

            await _db.SaveChangesAsync();
            return Unauthorized(new { message = "Invalid credentials." });
        }

        // Success reset
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;

        // RoadMap: 15 minutes
        var accessMinutes = _cfg.GetValue("Jwt:AccessTokenMinutes", 15);
        var accessToken = IssueJwt(user, accessMinutes);

        // Refresh token واقعی
        var refreshDays = _cfg.GetValue("Jwt:RefreshTokenDays", 30);

        var refreshRaw = GenerateRefreshTokenRaw();
        var refreshHash = Sha256Hex(refreshRaw);

        var refreshEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserAccountId = user.Id,
            TokenHash = refreshHash,
            CreatedUtc = now,
            ExpiresUtc = now.AddDays(refreshDays),
            RevokedUtc = null,
            RevokeReason = null,
            ReplacedByTokenId = null
        };

        _db.RefreshTokens.Add(refreshEntity);

        await _db.SaveChangesAsync();

        return Ok(new TokenResponse(
            access_token: accessToken,
            expires_in: accessMinutes * 60,
            refresh_token: refreshRaw,
            refresh_expires_in: refreshDays * 24 * 3600
        ));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> Refresh([FromBody] RefreshRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.refresh_token))
            return BadRequest(new { message = "refresh_token_required" });

        var now = DateTime.UtcNow;
        var oldHash = Sha256Hex(req.refresh_token);

        var oldToken = await _db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == oldHash);
        if (oldToken is null)
            return Unauthorized(new { message = "invalid_refresh" });

        // Refresh Token Reuse Detection:
        // اگر refresh token قبلاً revoke/rotate شده و دوباره ارائه شود،
        // این نشانه replay یا سرقت احتمالی token است.
        // در این حالت تمام refresh tokenهای فعال همان کاربر revoke می‌شوند.
        if (oldToken.RevokedUtc.HasValue)
        {
            await RevokeActiveRefreshTokensForUser(
                oldToken.UserAccountId,
                now,
                "reuse_detected",
                HttpContext.RequestAborted);

            return Unauthorized(new { message = "invalid_refresh" });
        }

        if (oldToken.ExpiresUtc <= now)
            return Unauthorized(new { message = "refresh_not_active" });

        var user = await _db.UserAccounts.SingleOrDefaultAsync(x => x.Id == oldToken.UserAccountId);
        if (user is null || !user.IsActive)
            return Unauthorized(new { message = "invalid_user" });

        // Rotate: revoke old + issue new
        var refreshDays = _cfg.GetValue("Jwt:RefreshTokenDays", 30);

        var newRaw = GenerateRefreshTokenRaw();
        var newHash = Sha256Hex(newRaw);

        var newToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserAccountId = user.Id,
            TokenHash = newHash,
            CreatedUtc = now,
            ExpiresUtc = now.AddDays(refreshDays),
            RevokedUtc = null,
            RevokeReason = null,
            ReplacedByTokenId = null
        };

        oldToken.RevokedUtc = now;
        oldToken.RevokeReason = "rotated";
        oldToken.ReplacedByTokenId = newToken.Id;

        _db.RefreshTokens.Add(newToken);
        await _db.SaveChangesAsync();

        var accessMinutes = _cfg.GetValue("Jwt:AccessTokenMinutes", 15);
        var accessToken = IssueJwt(user, accessMinutes);

        return Ok(new TokenResponse(
            access_token: accessToken,
            expires_in: accessMinutes * 60,
            refresh_token: newRaw,
            refresh_expires_in: refreshDays * 24 * 3600
        ));
    }

    [HttpPost("revoke")]
    [AllowAnonymous]
    public async Task<IActionResult> Revoke([FromBody] RevokeRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.refresh_token))
            return BadRequest(new { message = "refresh_token_required" });

        var now = DateTime.UtcNow;
        var hash = Sha256Hex(req.refresh_token);

        var token = await _db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash);

        // idempotent
        if (token is null)
            return Ok(new { ok = true });

        if (token.RevokedUtc is null)
        {
            token.RevokedUtc = now;
            token.RevokeReason = "manual_revoke";
            await _db.SaveChangesAsync();
        }

        return Ok(new { ok = true });
    }

    private async Task<int> RevokeActiveRefreshTokensForUser(
        Guid userAccountId,
        DateTime now,
        string reason,
        CancellationToken ct)
    {
        var activeTokens = await _db.RefreshTokens
            .Where(x =>
                x.UserAccountId == userAccountId &&
                x.RevokedUtc == null &&
                x.ExpiresUtc > now)
            .ToListAsync(ct);

        if (activeTokens.Count == 0)
        {
            return 0;
        }

        foreach (var token in activeTokens)
        {
            token.RevokedUtc = now;
            token.RevokeReason = reason;
        }

        return await _db.SaveChangesAsync(ct);
    }

    private static bool IsRefreshActive(RefreshToken t, DateTime utcNow)
        => t.RevokedUtc is null && utcNow < t.ExpiresUtc;

    private static string GenerateRefreshTokenRaw()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string Sha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private string IssueJwt(UserAccount user, int minutes)
    {
        var jwtKey = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY");
        if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
            throw new InvalidOperationException("JWT_SIGNING_KEY is missing or too short (min 32 chars).");

        var issuer = _cfg["Jwt:Issuer"];
        var audience = _cfg["Jwt:Audience"];
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("Jwt:Issuer / Jwt:Audience missing in configuration.");

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
        };

        var roles = (user.Roles ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (roles.Length == 0) roles = new[] { "User" };
        foreach (var r in roles)
            claims.Add(new(ClaimTypes.Role, r));

        if (user.CompanyCode is not null)
            claims.Add(new("company_code", user.CompanyCode.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(minutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}