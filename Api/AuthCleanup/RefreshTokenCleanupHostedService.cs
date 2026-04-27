using BonyadRazi.Portal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BonyadRazi.Portal.Api.AuthCleanup;

public sealed class RefreshTokenCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RefreshTokenCleanupHostedService> _logger;
    private readonly IOptionsMonitor<AuthCleanupOptions> _options;

    public RefreshTokenCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<RefreshTokenCleanupHostedService> logger,
        IOptionsMonitor<AuthCleanupOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var currentOptions = _options.CurrentValue;

            if (!currentOptions.Enabled)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                continue;
            }

            try
            {
                await RunOnce(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RefreshToken cleanup failed.");
            }

            var delay = TimeSpan.FromMinutes(Math.Max(1, currentOptions.RunEveryMinutes));
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task RunOnce(CancellationToken cancellationToken)
    {
        var currentOptions = _options.CurrentValue;
        var now = DateTime.UtcNow;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RasfPortalDbContext>();

        var deletedExpired = 0;
        var deletedOldRevoked = 0;

        if (currentOptions.DeleteExpired)
        {
            deletedExpired = await db.RefreshTokens
                .Where(x => x.ExpiresUtc <= now)
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (currentOptions.DeleteRevokedOlderThanDays > 0)
        {
            var cutoff = now.AddDays(-currentOptions.DeleteRevokedOlderThanDays);

            deletedOldRevoked = await db.RefreshTokens
                .Where(x => x.RevokedUtc != null && x.RevokedUtc <= cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (deletedExpired > 0 || deletedOldRevoked > 0)
        {
            _logger.LogInformation(
                "RefreshToken cleanup done. deletedExpired={deletedExpired}, deletedOldRevoked={deletedOldRevoked}, utc={utc}",
                deletedExpired,
                deletedOldRevoked,
                now);
        }
    }
}