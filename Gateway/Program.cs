using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// --------------------
// Config + YARP
// --------------------
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// --------------------
// JWT validation at Gateway (optional by config flag)
// --------------------
var jwtKey = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY");
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("JWT_SIGNING_KEY is missing or too short (min 32 chars).");

var issuer = builder.Configuration["Jwt:Issuer"];
var audience = builder.Configuration["Jwt:Audience"];
if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
    throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must be provided in Gateway configuration.");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
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

builder.Services.AddAuthorization();

// --------------------
// Rate Limiting (global path-aware limiter)
// - login: 10/min/IP
// - refresh/revoke: 30/min/IP
// --------------------
builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    opt.OnRejected = async (ctx, ct) =>
    {
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var ra))
            ctx.HttpContext.Response.Headers["Retry-After"] = ((int)ra.TotalSeconds).ToString();

        var logger = ctx.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("RateLimit");

        logger.LogWarning("Rate limited: ip={ip} path={path} trace={traceId}",
            ctx.HttpContext.Connection.RemoteIpAddress?.ToString(),
            ctx.HttpContext.Request.Path.ToString(),
            ctx.HttpContext.TraceIdentifier);

        await ctx.HttpContext.Response.WriteAsJsonAsync(new
        {
            message = "rate_limited",
            traceId = ctx.HttpContext.TraceIdentifier
        }, ct);
    };

    opt.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
    {
        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var path = http.Request.Path.Value ?? "";

        if (path.StartsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
        }

        if (path.StartsWith("/api/auth/refresh", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/auth/revoke", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
        }

        // everything else: no limiter
        return RateLimitPartition.GetNoLimiter("nolimit");
    });
});

var app = builder.Build();

// --------------------
// Public health
// --------------------
app.MapGet("/gateway/health", () =>
{
    return Results.Json(new
    {
        status = "ok",
        where = "gateway",
        utc = DateTime.UtcNow
    });
});

// --------------------
// Middleware order
// --------------------
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// --------------------
// Enforce JWT for /api/* except allowlist when ValidateJwtAtGateway=true
// --------------------
bool validateAtGateway = builder.Configuration.GetValue<bool>("Security:ValidateJwtAtGateway");

static bool IsAuthAllowListed(PathString path) =>
    path.StartsWithSegments("/api/auth/login") ||
    path.StartsWithSegments("/api/auth/refresh") ||
    path.StartsWithSegments("/api/auth/revoke");

app.Use(async (ctx, next) =>
{
    if (!validateAtGateway)
    {
        await next();
        return;
    }

    // only protect /api/*
    if (!ctx.Request.Path.StartsWithSegments("/api"))
    {
        await next();
        return;
    }

    // allow anonymous auth endpoints
    if (IsAuthAllowListed(ctx.Request.Path))
    {
        await next();
        return;
    }

    // enforce auth
    var result = await ctx.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
    if (!result.Succeeded || result.Principal is null)
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await ctx.Response.WriteAsJsonAsync(new { message = "unauthorized" });
        return;
    }

    ctx.User = result.Principal;
    await next();
});

// --------------------------------------------------------------------
// ✅ TESTING MODE (no downstream):
// if env=Testing OR config Security:ForceTestingEndpoints=true,
// do NOT call MapReverseProxy(). Instead, expose stub endpoints.
// --------------------------------------------------------------------
var forceTestingEndpoints = app.Configuration.GetValue("Security:ForceTestingEndpoints", false);
var useTestingEndpoints = app.Environment.IsEnvironment("Testing") || forceTestingEndpoints;

if (useTestingEndpoints)
{
    // NOTE: these are only for GatewayTests to avoid 504 due to missing upstream
    app.MapGet("/api/companies/{**catchAll}", (HttpContext ctx) =>
        Results.Ok(new { ok = true, path = ctx.Request.Path.Value }));

    app.MapPost("/api/auth/login", () => Results.Ok(new { ok = true }));
    app.MapPost("/api/auth/refresh", () => Results.Ok(new { ok = true }));
    app.MapPost("/api/auth/revoke", () => Results.Ok(new { ok = true }));
}
else
{
    // --------------------
    // Reverse Proxy
    // --------------------
    app.MapReverseProxy();
}

app.Run();

public partial class Program { }