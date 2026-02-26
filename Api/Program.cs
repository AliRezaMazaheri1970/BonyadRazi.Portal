using BonyadRazi.Portal.Api.Audit;
using BonyadRazi.Portal.Api.Middleware;
using BonyadRazi.Portal.Api.Security;
using BonyadRazi.Portal.Application.Abstractions;
using BonyadRazi.Portal.Infrastructure.Audit;
using BonyadRazi.Portal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ---- Secrets Hygiene: JWT key only from env ----
var jwtKey = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY");
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("JWT_SIGNING_KEY is missing or too short (min 32 chars).");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

var issuer = builder.Configuration["Jwt:Issuer"];
var audience = builder.Configuration["Jwt:Audience"];
if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
    throw new InvalidOperationException("Jwt:Issuer / Jwt:Audience missing in configuration.");

// ---- DB (RasfPorta) ----
builder.Services.AddDbContext<RasfPortalDbContext>(opt =>
{
    opt.UseSqlServer(builder.Configuration.GetConnectionString("RasfPorta"));
});

// ---- Authentication ----
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.RequireHttpsMetadata =
            !builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing");

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

// ---- Authorization ----
builder.Services.AddAuthorization(options =>
{
    // Default deny (fallback): everything requires auth unless explicitly allowed
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    // Register all policies (Admin/SuperAdmin by default)
    PortalPolicies.AddPortalPolicies(options);

    // Override CompaniesRead to also require tenant claim
    options.AddPolicy(PortalPolicies.CompaniesRead, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(PortalClaims.CompanyCode);
        policy.RequireRole("Admin", "SuperAdmin");
    });
});

// ---- Audit logging service ----
if (builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddSingleton<IUserActionLogService, NoOpUserActionLogService>();
else
    builder.Services.AddScoped<IUserActionLogService, DbUserActionLogService>();

builder.Services.AddSingleton<Pbkdf2PasswordHasher>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RasfPortalDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<Pbkdf2PasswordHasher>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DevSeed");

    try
    {
        // اگر migration اجرا نشده باشد، این بخش ممکن است خطا بدهد.
        if (!await db.UserAccounts.AnyAsync(x => x.Username == "admin"))
        {
            var (hash, salt, it) = hasher.Hash("admin");

            db.UserAccounts.Add(new BonyadRazi.Portal.Infrastructure.Auth.Entities.UserAccount
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
app.UseMiddleware<SecurityDeniedAuditMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.Run();

public partial class Program { }