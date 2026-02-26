using BonyadRazi.Portal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BonyadRazi.Portal.Api.AuthCleanup;

public sealed class RefreshTokenCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RefreshTokenCleanupHostedService> _logger;
    private readonly IOptionsMonitor<AuthCleanupOptions> _opt;

    public RefreshTokenCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<RefreshTokenCleanupHostedService> logger,
        IOptionsMonitor<AuthCleanupOptions> opt)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _opt = opt;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // PeriodicTimer دقیق و کم‌هزینه
        while (!stoppingToken.IsCancellationRequested)
        {
            var o = _opt.CurrentValue;
            if (!o.Enabled)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                continue;
            }

            try
            {
                await RunOnce(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RefreshToken cleanup failed.");
            }

            var delay = TimeSpan.FromMinutes(Math.Max(1, o.RunEveryMinutes));
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task RunOnce(CancellationToken ct)
    {
        var o = _opt.CurrentValue;
        var now = DateTime.UtcNow;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RasfPortalDbContext>();

        int deletedExpired = 0;
        int deletedOldRevoked = 0;

        if (o.DeleteExpired)
        {
            deletedExpired = await db.RefreshTokens
                .Where(x => x.ExpiresUtc <= now)
                .ExecuteDeleteAsync(ct);
        }

        if (o.DeleteRevokedOlderThanDays > 0)
        {
            var cutoff = now.AddDays(-o.DeleteRevokedOlderThanDays);

            deletedOldRevoked = await db.RefreshTokens
                .Where(x => x.RevokedUtc != null && x.RevokedUtc <= cutoff)
                .ExecuteDeleteAsync(ct);
        }

        if (deletedExpired > 0 || deletedOldRevoked > 0)
        {
            _logger.LogInformation(
                "RefreshToken cleanup done. deletedExpired={deletedExpired}, deletedOldRevoked={deletedOldRevoked}, utc={utc}",
                deletedExpired, deletedOldRevoked, now);
        }
    }
}