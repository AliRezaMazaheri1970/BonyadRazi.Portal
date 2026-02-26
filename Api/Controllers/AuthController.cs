using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BonyadRazi.Portal.Api.Security;
using BonyadRazi.Portal.Infrastructure.Auth.Entities;
using BonyadRazi.Portal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace BonyadRazi.Portal.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly RasfPortalDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly Pbkdf2PasswordHasher _hasher;

    // Lockout policy (Dev)
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    public AuthController(RasfPortalDbContext db, IConfiguration cfg, Pbkdf2PasswordHasher hasher)
    {
        _db = db;
        _cfg = cfg;
        _hasher = hasher;
    }

    public sealed record LoginRequest(string Username, string Password);
    public sealed record TokenResponse(string access_token, int expires_in);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> Login([FromBody] LoginRequest req)
    {
        var username = (req.Username ?? "").Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(req.Password))
            return Unauthorized(new { message = "Invalid credentials." });

        var user = await _db.UserAccounts.SingleOrDefaultAsync(x => x.Username == username);

        // جلوگیری از user-enumeration
        if (user is null || !user.IsActive)
            return Unauthorized(new { message = "Invalid credentials." });

        var now = DateTime.UtcNow;

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
        await _db.SaveChangesAsync();

        // ✅ RoadMap: 15 minutes
        var minutes = _cfg.GetValue("Jwt:AccessTokenMinutes", 15);

        var token = IssueJwt(user, minutes);

        return Ok(new TokenResponse(token, expires_in: minutes * 60));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public IActionResult Refresh()
    {
        // در Dev فعلاً refresh ساده است (بعداً می‌تونی واقعی‌اش کنی)
        return Unauthorized(new { message = "Not implemented in dev-db mode. Use login." });
    }

    [HttpPost("revoke")]
    [AllowAnonymous]
    public IActionResult Revoke()
    {
        // در Dev فعلاً revoke ساده است (بعداً می‌تونی واقعی‌اش کنی)
        return Ok(new { ok = true });
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

        // نقش‌ها: اگر چندتا نقش داری با کاما جدا کردی، چند claim بساز
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