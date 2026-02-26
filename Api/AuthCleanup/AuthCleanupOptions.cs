namespace BonyadRazi.Portal.Api.AuthCleanup;

public sealed class AuthCleanupOptions
{
    public bool Enabled { get; set; } = true;
    public int RunEveryMinutes { get; set; } = 1440;
    public bool DeleteExpired { get; set; } = true;
    public int DeleteRevokedOlderThanDays { get; set; } = 30;
}