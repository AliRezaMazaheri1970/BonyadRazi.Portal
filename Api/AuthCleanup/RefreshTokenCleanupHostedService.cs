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
            var expiredQuery = db.RefreshTokens
                .Where(x => x.ExpiresUtc <= now);

            deletedExpired = await DeleteRefreshTokensAsync(
                db,
                expiredQuery,
                cancellationToken);
        }

        if (currentOptions.DeleteRevokedOlderThanDays > 0)
        {
            var cutoff = now.AddDays(-currentOptions.DeleteRevokedOlderThanDays);

            var oldRevokedQuery = db.RefreshTokens
                .Where(x => x.RevokedUtc != null && x.RevokedUtc <= cutoff);

            deletedOldRevoked = await DeleteRefreshTokensAsync(
                db,
                oldRevokedQuery,
                cancellationToken);
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

    private static async Task<int> DeleteRefreshTokensAsync<T>(
        RasfPortalDbContext db,
        IQueryable<T> query,
        CancellationToken cancellationToken)
        where T : class
    {
        var providerName = db.Database.ProviderName ?? string.Empty;

        if (providerName.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            var entities = await query.ToListAsync(cancellationToken);

            if (entities.Count == 0)
            {
                return 0;
            }

            db.RemoveRange(entities);
            return await db.SaveChangesAsync(cancellationToken);
        }

        return await query.ExecuteDeleteAsync(cancellationToken);
    }
}