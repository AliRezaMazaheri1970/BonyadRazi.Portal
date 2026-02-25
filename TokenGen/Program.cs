using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

var jwtKey = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY")
             ?? throw new Exception("JWT_SIGNING_KEY is missing.");

if (jwtKey.Length < 32)
    throw new Exception("JWT_SIGNING_KEY must be at least 32 characters.");

var issuer = Environment.GetEnvironmentVariable("Jwt__Issuer") ?? "BonyadRazi.Auth";
var audience = Environment.GetEnvironmentVariable("Jwt__Audience") ?? "BonyadRazi.Portal";

// یک company_code نمونه (می‌تونی عوضش کنی)
var companyCode = "89467084-2A33-4054-8418-97E5E59ED17F";

// برای اینکه مطمئن شی چی تولید میشه:
Console.WriteLine($"Issuer   : {issuer}");
Console.WriteLine($"Audience : {audience}");
Console.WriteLine($"Company  : {companyCode}");

var claims = new List<Claim>
{
    new("sub", "1"),
    new("company_code", companyCode),
    new Claim(ClaimTypes.Role, "Admin"),
};

var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

var token = new JwtSecurityToken(
    issuer: issuer,
    audience: audience,
    claims: claims,
    notBefore: DateTime.UtcNow,
    expires: DateTime.UtcNow.AddHours(2),
    signingCredentials: creds
);

var jwt = new JwtSecurityTokenHandler().WriteToken(token);
Console.WriteLine();
Console.WriteLine("JWT:");
Console.WriteLine(jwt);