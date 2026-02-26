using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace BonyadRazi.Portal.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IConfiguration _cfg;

    // یک کاربر Dev برای تست
    private static readonly Guid DevAdminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DevCompanyCode = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public AuthController(IConfiguration cfg)
    {
        _cfg = cfg;
    }

    public sealed record LoginRequest(string Username, string Password);

    public sealed record TokenResponse(string access_token, int expires_in);

    [HttpPost("login")]
    [AllowAnonymous]
    public ActionResult<TokenResponse> Login([FromBody] LoginRequest req)
    {
        // TODO: بعداً این قسمت را به DB/Identity وصل کن
        if (!string.Equals(req.Username, "admin", StringComparison.OrdinalIgnoreCase) ||
            req.Password != "admin")
        {
            return Unauthorized(new { message = "Invalid credentials." });
        }

        var token = IssueJwt(
            userId: DevAdminUserId,
            role: "Admin",
            companyCode: DevCompanyCode,
            minutes: _cfg.GetValue("Jwt:AccessTokenMinutes", 30)
        );

        return Ok(new TokenResponse(token, expires_in: _cfg.GetValue("Jwt:AccessTokenMinutes", 30) * 60));
    }

    // برای Dev ساده: refresh هم یک JWT جدید می‌دهد
    [HttpPost("refresh")]
    [AllowAnonymous]
    public ActionResult<TokenResponse> Refresh()
    {
        var token = IssueJwt(
            userId: DevAdminUserId,
            role: "Admin",
            companyCode: DevCompanyCode,
            minutes: _cfg.GetValue("Jwt:AccessTokenMinutes", 30)
        );

        return Ok(new TokenResponse(token, expires_in: _cfg.GetValue("Jwt:AccessTokenMinutes", 30) * 60));
    }

    // برای Dev: revoke صرفاً OK
    [HttpPost("revoke")]
    [AllowAnonymous]
    public IActionResult Revoke()
    {
        return Ok(new { ok = true });
    }

    private string IssueJwt(Guid userId, string role, Guid companyCode, int minutes)
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

        // IMPORTANT: MapInboundClaims=false است، پس این claimها همانطور می‌مانند.
        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),                // NameClaimType = "sub"
            new(ClaimTypes.Role, role),                   // RoleClaimType = ClaimTypes.Role
            new("company_code", companyCode.ToString())   // برای tenant isolation / CompaniesRead
        };

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