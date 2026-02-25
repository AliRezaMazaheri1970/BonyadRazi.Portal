using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BonyadRazi.Portal.SecurityTests;

internal static class JwtTestToken
{
    public static string Create(
        Guid userId,
        Guid companyCode,
        string[] roles,
        string issuer,
        string audience,
        TimeSpan? lifetime = null)
    {
        var key = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY");
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
            throw new InvalidOperationException("JWT_SIGNING_KEY is missing or too short (min 32 chars). Set it in test environment.");

        lifetime ??= TimeSpan.FromMinutes(10);

        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new("company_code", companyCode.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var r in roles)
        {
            // match Api TokenValidationParameters.RoleClaimType = ClaimTypes.Role
            claims.Add(new Claim(ClaimTypes.Role, r));
            // extra compatibility claim (harmless)
            claims.Add(new Claim("role", r));
        }

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now.AddSeconds(-5),
            expires: now.Add(lifetime.Value),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
