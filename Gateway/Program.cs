using BonyadRazi.Portal.Api.Security;
using Gateway;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ---------------- Reverse Proxy (YARP) ----------------
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// ---------------- Forwarded Headers (real client IP behind proxy) ----------------
// IMPORTANT: If Gateway is behind another proxy/load balancer, this allows reading X-Forwarded-For.
// For safety, consider configuring KnownProxies/KnownNetworks in production.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;

    // ✅ .NET 10: use KnownIPNetworks (System.Net.IPNetwork)
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();

    // اگر قبلاً KnownNetworks.Clear می‌کردی برای پذیرش همه‌ی پراکسی‌ها،
    // بهتره در PROD به جای "clear کردن همه"، KnownProxies/KnownIPNetworks رو دقیق ست کنی.
});

// ---------------- Rate Limiting (Global, path-based) ----------------
builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    opt.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        // NOTE: GatewaySecurityHelper should read the *forwarded* IP correctly after UseForwardedHeaders().
        var ip = GatewaySecurityHelper.GetClientIp(ctx)?.ToString() ?? "unknown";
        var path = ctx.Request.Path;

        string bucket;
        int permitLimit;

        if (path.StartsWithSegments("/api/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            bucket = "login";
            permitLimit = 10;
        }
        else if (path.StartsWithSegments("/api/auth/refresh", StringComparison.OrdinalIgnoreCase) ||
                 path.StartsWithSegments("/api/auth/revoke", StringComparison.OrdinalIgnoreCase))
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

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opt =>
        {
            // In tests (WebApplicationFactory/TestServer) HTTPS metadata often isn't available.
            opt.RequireHttpsMetadata = !builder.Environment.IsEnvironment("Testing");

            opt.MapInboundClaims = false;
            opt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,

                ValidateIssuer = true,
                ValidIssuer = issuer,

                ValidateAudience = true,
                ValidAudience = audience,

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),

                RoleClaimType = ClaimTypes.Role,
                NameClaimType = "sub"
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        // ✅ NO FallbackPolicy in Gateway (avoid breaking public endpoints like /gateway/health)
        // Keep explicit policies if you want to use them on specific endpoints later.

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

// IMPORTANT: Must be before anything that reads client IP.
app.UseForwardedHeaders();

// Global rate limiter
app.UseRateLimiter();

// ---------------- Default Deny (Path Allowlist) + IP Allowlist ----------------
app.Use(async (HttpContext ctx, RequestDelegate next) =>
{
    // Allow ONLY gateway health always.
    if (ctx.Request.Path.StartsWithSegments("/gateway/health", StringComparison.OrdinalIgnoreCase))
    {
        await next(ctx);
        return;
    }

    // 1) API Allow prefixes (protects "only known prefixes are served")
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
   .AllowAnonymous()
   .DisableRateLimiting();

// ---------------- Testing endpoints (no downstream dependency) ----------------
if (app.Environment.IsEnvironment("Testing"))
{
    // These endpoints exist only to validate gateway rules.
    app.MapGet("/health", () => Results.Ok(new { ok = true, where = "gateway-testing" }));

    app.MapGet("/api/companies/{**catchAll}", (HttpContext ctx) =>
        Results.Ok(new
        {
            ok = true,
            path = ctx.Request.Path.Value,
            auth = ctx.Request.Headers.Authorization.ToString()
        }));

    app.MapPost("/api/auth/login", () => Results.Ok(new { ok = true, route = "login" }));
    app.MapPost("/api/auth/refresh", () => Results.Ok(new { ok = true, route = "refresh" }));
    app.MapPost("/api/auth/revoke", () => Results.Ok(new { ok = true, route = "revoke" }));
}
else
{
    // ---------------- Reverse Proxy ----------------
    app.MapReverseProxy();
}

app.Run();

// Needed for WebApplicationFactory<T>
public partial class Program { }