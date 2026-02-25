using BonyadRazi.Portal.Api.Security;
using Gateway;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ---------------- Reverse Proxy (YARP) ----------------
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// ---------------- Rate Limiting (Global, path-based) ----------------
// Policy selection by path:
// - /api/auth/login  => 10/min/IP
// - /api/auth/refresh|/api/auth/revoke => 20/min/IP
// - /api/* => 120/min/IP
builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    opt.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var ip = GatewaySecurityHelper.GetClientIp(ctx)?.ToString() ?? "unknown";
        var path = ctx.Request.Path;

        string bucket;
        int permitLimit;

        if (path.StartsWithSegments("/api/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            bucket = "login";
            permitLimit = 10;
        }
        else if (
            path.StartsWithSegments("/api/auth/refresh", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/api/auth/revoke", StringComparison.OrdinalIgnoreCase)
        )
        {
            bucket = "refresh";
            permitLimit = 20;
        }
        else if (path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            bucket = "standard";
            permitLimit = 120;
        }
        else
        {
            // non-api endpoints (health, etc.) → no limit
            return RateLimitPartition.GetNoLimiter("no-limit");
        }

        var key = $"{ip}:{bucket}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: key,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

// ---------------- Optional JWT Validation at Gateway ----------------
var validateAtGateway = builder.Configuration.GetValue("Security:ValidateJwtAtGateway", true);

if (validateAtGateway)
{
    var jwtKey = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY");
    if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
        throw new InvalidOperationException("JWT_SIGNING_KEY is missing or too short (min 32 chars).");

    var issuer = builder.Configuration["Jwt:Issuer"];
    var audience = builder.Configuration["Jwt:Audience"];
    if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
        throw new InvalidOperationException("Jwt:Issuer / Jwt:Audience missing in configuration.");

    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        // درستش اینه:
        PortalPolicies.AddPortalPolicies(options);

        // Override CompaniesRead: هم Role هم Claim
        options.AddPolicy(PortalPolicies.CompaniesRead, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim(PortalClaims.CompanyCode);
            policy.RequireRole("Admin", "SuperAdmin");
        });
    });
}

var app = builder.Build();

// Avoid redirect issues in local/TestServer scenarios.
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
    app.UseHttpsRedirection();

// Global rate limiter
app.UseRateLimiter();

// ---------------- Default Deny (Path Allowlist) + IP Allowlist ----------------
app.Use(async (HttpContext ctx, RequestDelegate next) =>
{
    // Allow ONLY gateway health always.
    // Do NOT bypass /health here; /health is proxied and should respect allowlist/IP/JWT rules.
    if (ctx.Request.Path.StartsWithSegments("/gateway/health", StringComparison.OrdinalIgnoreCase))
    {
        await next(ctx);
        return;
    }

    // 1) API Allow prefixes
    var prefixes = app.Configuration
        .GetSection("Security:ApiAllowPrefixes")
        .Get<string[]>() ?? Array.Empty<string>();

    if (prefixes.Length > 0)
    {
        var ok = prefixes.Any(p => ctx.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
        if (!ok)
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            await ctx.Response.WriteAsync("Not found.");
            return;
        }
    }

    // 2) IP Allowlist (CIDR)
    if (!GatewaySecurityHelper.IsIpAllowed(ctx, app.Configuration, app.Environment))
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        await ctx.Response.WriteAsync("Forbidden.");
        return;
    }

    await next(ctx);
});

if (validateAtGateway)
{
    app.UseAuthentication();
    app.UseAuthorization();

    // Require valid JWT for protected APIs (but allow public auth endpoints)
    app.Use(async (HttpContext ctx, RequestDelegate next) =>
    {
        // Protected endpoints:
        // - /api/* protected except public auth endpoints
        // - /health also protected (proxied health)
        if (ctx.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ||
            ctx.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            // Public endpoints (no JWT needed)
            if (ctx.Request.Path.StartsWithSegments("/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
                ctx.Request.Path.StartsWithSegments("/api/auth/refresh", StringComparison.OrdinalIgnoreCase) ||
                ctx.Request.Path.StartsWithSegments("/api/auth/revoke", StringComparison.OrdinalIgnoreCase))
            {
                await next(ctx);
                return;
            }

            var result = await ctx.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            if (!result.Succeeded)
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsync("Unauthorized.");
                return;
            }
        }

        await next(ctx);
    });
}

// ---------------- Health ----------------
app.MapGet("/gateway/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow }))
   .DisableRateLimiting();

// ---------------- Reverse Proxy ----------------
app.MapReverseProxy();

app.Run();
