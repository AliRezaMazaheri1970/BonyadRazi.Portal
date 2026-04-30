namespace BonyadRazi.Portal.Api.Security;

public sealed record UsernameLoginRateLimitResult(
    bool Allowed,
    int RetryAfterSeconds)
{
    public static UsernameLoginRateLimitResult Allow() => new(true, 0);

    public static UsernameLoginRateLimitResult Deny(int retryAfterSeconds)
        => new(false, Math.Max(1, retryAfterSeconds));
}