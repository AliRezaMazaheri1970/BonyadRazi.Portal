/*
Program.cs - Composition Root and Middleware Pipeline

This file configures the application's composition root: dependency injection, configuration validation,
authentication and authorization schemes, database context registration, and audit logging services.
The middleware pipeline enforces authentication and authorization and registers a custom security audit
middleware. Top-level statements are used to host the application with minimal API style entry point.
*/

using BonyadRazi.Portal.Api.Audit;
using BonyadRazi.Portal.Api.AuthCleanup;
using BonyadRazi.Portal.Api.Middleware;
using BonyadRazi.Portal.Api.Security;
using BonyadRazi.Portal.Application.Abstractions;
using BonyadRazi.Portal.Infrastructure.Audit;
using BonyadRazi.Portal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    options.Filters.Add<TenantConsistencyFilter>();
});

builder.Services.AddOpenApi();

// --------------------
// Forwarded Headers
// --------------------
// API is behind Gateway/YARP.
// Trust only configured proxies so arbitrary clients cannot spoof X-Forwarded-For.
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
// JWT
// --------------------
var jwtKey = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY");
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("JWT_SIGNING_KEY is missing or too short (minimum 32 characters).");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

var issuer = builder.Configuration["Jwt:Issuer"];
var audience = builder.Configuration["Jwt:Audience"];
if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
    throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must be provided in configuration.");

// --------------------
// Persistence
// --------------------
builder.Services.AddDbContext<RasfPortalDbContext>(opt =>
{
    opt.UseSqlServer(builder.Configuration.GetConnectionString("RasfPorta"));
});

// --------------------
// Authentication / Authorization
// --------------------
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

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    PortalPolicies.AddPortalPolicies(options);
});

// --------------------
// Background jobs / Services
// --------------------
builder.Services.Configure<AuthCleanupOptions>(builder.Configuration.GetSection("AuthCleanup"));
builder.Services.AddHostedService<RefreshTokenCleanupHostedService>();

if (builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddSingleton<IUserActionLogService, NoOpUserActionLogService>();
else
    builder.Services.AddScoped<IUserActionLogService, DbUserActionLogService>();

builder.Services.AddSingleton<Pbkdf2PasswordHasher>();

var app = builder.Build();

// IMPORTANT:
// This must run before anything that reads RemoteIpAddress:
// authentication, audit middleware, authorization, controllers.
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RasfPortalDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<Pbkdf2PasswordHasher>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DevSeed");

    try
    {
        // Development-only: create a default administrator account if migrations have been applied.
        if (!await db.UserAccounts.AnyAsync(x => x.Username == "admin"))
        {
            var (hash, salt, it) = hasher.Hash("admin");

            db.UserAccounts.Add(new BonyadRazi.Portal.Infrastructure.Audit.Entities.UserAccount
            {
                Username = "admin",
                PasswordHash = hash,
                PasswordSalt = salt,
                PasswordIterations = it,
                Roles = "Admin",
                CompanyCode = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                IsActive = true
            });

            await db.SaveChangesAsync();
            logger.LogInformation("DevSeed: admin user created.");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "DevSeed skipped (likely migrations not applied yet).");
    }
}

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseDeveloperExceptionPage();
}

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();

// Must be after UseAuthentication so user/claims are available,
// and before UseAuthorization so 401/403 responses are captured.
app.UseMiddleware<SecurityDeniedAuditMiddleware>();

app.UseAuthorization();

// Public API health endpoint used by deployment smoke tests.
// Keep the response minimal: no configuration, secrets, database details, or machine internals.
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    where = "api",
    utc = DateTime.UtcNow
})).AllowAnonymous();

app.MapControllers();

app.Run();

/// <summary>
/// Program entry point partial class used to enable integration testing via <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/>.
/// </summary>
public partial class Program { }