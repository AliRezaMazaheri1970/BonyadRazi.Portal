using Gateway;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// --------------------
// Reverse Proxy (YARP)
// --------------------
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// --------------------
// Forwarded Headers
// --------------------
// Gateway is behind HAProxy.
// Trust only configured proxies/networks so clients cannot spoof X-Forwarded-* headers.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;

    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();

    options.RequireHeaderSymmetry = builder.Configuration.GetValue(
        "ForwardedHeaders:RequireHeaderSymmetry",
        true);

    options.ForwardLimit = builder.Configuration.GetValue(
        "ForwardedHeaders:ForwardLimit",
        1);

    var proxies = builder.Configuration
        .GetSection("ForwardedHeaders:KnownProxies")
        .Get<string[]>() ?? Array.Empty<string>();

    foreach (var proxy in proxies)
    {
        if (IPAddress.TryParse(proxy, out var ip))
        {
            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();

            options.KnownProxies.Add(ip);
        }
    }

    var cidrs = builder.Configuration
        .GetSection("ForwardedHeaders:KnownCidrs")
        .Get<string[]>() ?? Array.Empty<string>();

    foreach (var cidr in cidrs)
    {
        var parts = cidr.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length != 2)
            continue;

        if (!IPAddress.TryParse(parts[0], out var networkIp))
            continue;

        if (!int.TryParse(parts[1], out var prefixLength))
            continue;

        if (networkIp.IsIPv4MappedToIPv6)
            networkIp = networkIp.MapToIPv4();

        options.KnownIPNetworks.Add(
            new System.Net.IPNetwork(networkIp, prefixLength));
    }
});

// --------------------
// JWT validation at Gateway
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
        opt.RequireHttpsMetadata =
            !builder.Environment.IsDevelopment() &&
            !builder.Environment.IsEnvironment("Testing");

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
// Rate Limiting
// --------------------
// - /api/auth/login          => 10/min/IP
// - /api/auth/refresh|revoke => 30/min/IP
// - other /api/*             => 120/min/IP
builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    opt.OnRejected = async (ctx, ct) =>
    {
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            ctx.HttpContext.Response.Headers["Retry-After"] =
                ((int)retryAfter.TotalSeconds).ToString();
        }

        var logger = ctx.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Gateway.RateLimit");

        var ip = GatewaySecurityHelper.GetClientIp(ctx.HttpContext)?.ToString() ?? "unknown";

        logger.LogWarning(
            "Rate limited: ip={ip} path={path} traceId={traceId}",
            ip,
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
        var ip = GatewaySecurityHelper.GetClientIp(http)?.ToString() ?? "unknown";
        var path = http.Request.Path.Value ?? "";

        if (path.StartsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"{ip}:login",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                });
        }

        if (path.StartsWith("/api/auth/refresh", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/auth/revoke", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"{ip}:refresh",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                });
        }

        if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"{ip}:api-standard",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                });
        }

        return RateLimitPartition.GetNoLimiter("no-limit");
    });
});

var app = builder.Build();

// IMPORTANT:
// This must run before anything that reads RemoteIpAddress.
// After this middleware, ctx.Connection.RemoteIpAddress becomes the trusted client IP
// if the request came from a configured KnownProxy/KnownNetwork.
app.UseForwardedHeaders();

// --------------------
// Forward real client IP from Gateway/YARP to downstream API
// --------------------
// Gateway connects to API over 127.0.0.1, so API would otherwise see only 127.0.0.1.
// After UseForwardedHeaders(), RemoteIpAddress is already the trusted client IP.
// We rewrite X-Forwarded-* before YARP proxies to API.
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path;

    var shouldForwardClientHeaders =
        path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/health", StringComparison.OrdinalIgnoreCase);

    if (shouldForwardClientHeaders)
    {
        var ip = ctx.Connection.RemoteIpAddress;

        if (ip is not null)
        {
            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();

            var clientIp = ip.ToString();

            ctx.Request.Headers["X-Forwarded-For"] = clientIp;
            ctx.Request.Headers["X-Real-IP"] = clientIp;
        }

        ctx.Request.Headers["X-Forwarded-Proto"] = ctx.Request.Scheme;
        ctx.Request.Headers["X-Forwarded-Host"] = ctx.Request.Host.Value;
    }

    await next();
});

// --------------------
// Public Gateway Health
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
// API Allowlist + IP Allowlist
// --------------------
app.Use(async (ctx, next) =>
{
    // /gateway/health is always public.
    if (ctx.Request.Path.StartsWithSegments("/gateway/health", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    var path = ctx.Request.Path;

    // Apply API allowlist only to API/service paths.
    // Do not block future WebApp/static routes here.
    var shouldCheckApiAllowlist =
        path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/health", StringComparison.OrdinalIgnoreCase);

    if (shouldCheckApiAllowlist)
    {
        var prefixes = app.Configuration
            .GetSection("Security:ApiAllowPrefixes")
            .Get<string[]>() ?? Array.Empty<string>();

        if (!GatewaySecurityHelper.IsPathAllowedByPrefixes(path, prefixes))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            await ctx.Response.WriteAsJsonAsync(new
            {
                message = "not_found",
                traceId = ctx.TraceIdentifier
            });
            return;
        }

        if (!GatewaySecurityHelper.IsIpAllowed(ctx, app.Configuration, app.Environment))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new
            {
                message = "forbidden",
                traceId = ctx.TraceIdentifier
            });
            return;
        }
    }

    await next();
});

// --------------------
// Enforce JWT for protected API paths
// --------------------
var validateAtGateway = app.Configuration.GetValue("Security:ValidateJwtAtGateway", true);

static bool IsPublicAuthEndpoint(PathString path)
{
    return path.StartsWithSegments("/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/api/auth/refresh", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/api/auth/revoke", StringComparison.OrdinalIgnoreCase);
}

app.Use(async (ctx, next) =>
{
    if (!validateAtGateway)
    {
        await next();
        return;
    }

    var path = ctx.Request.Path;

    var shouldRequireJwt =
        path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/health", StringComparison.OrdinalIgnoreCase);

    if (!shouldRequireJwt)
    {
        await next();
        return;
    }

    if (IsPublicAuthEndpoint(path))
    {
        await next();
        return;
    }

    var result = await ctx.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);

    if (!result.Succeeded || result.Principal is null)
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await ctx.Response.WriteAsJsonAsync(new
        {
            message = "unauthorized",
            traceId = ctx.TraceIdentifier
        });
        return;
    }

    ctx.User = result.Principal;
    await next();
});

// --------------------------------------------------------------------
// Testing Mode
// --------------------------------------------------------------------
// In Testing, do not call MapReverseProxy because upstream services are
// not running in CI. Stub endpoints are enough for Gateway security tests.
var forceTestingEndpoints = app.Configuration.GetValue("Security:ForceTestingEndpoints", false);
var useTestingEndpoints = app.Environment.IsEnvironment("Testing") || forceTestingEndpoints;

if (useTestingEndpoints)
{
    app.MapGet("/api/companies/{**catchAll}", (HttpContext ctx) =>
        Results.Ok(new
        {
            ok = true,
            path = ctx.Request.Path.Value
        }));

    app.MapGet("/api/diagnostics/auth-test", (HttpContext ctx) =>
        Results.Ok(new
        {
            ok = true,
            route = "diagnostics-auth-test",
            user = ctx.User.Identity?.Name,
            path = ctx.Request.Path.Value
        }));

    app.MapPost("/api/auth/login", () =>
        Results.Ok(new { ok = true, route = "login" }));

    app.MapPost("/api/auth/refresh", () =>
        Results.Ok(new { ok = true, route = "refresh" }));

    app.MapPost("/api/auth/revoke", () =>
        Results.Ok(new { ok = true, route = "revoke" }));
}
else
{
    app.MapReverseProxy();
}

app.Run();

public partial class Program { }