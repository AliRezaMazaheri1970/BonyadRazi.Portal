namespace BonyadRazi.Portal.Api.Security;

public interface IUsernameLoginRateLimiter
{
    UsernameLoginRateLimitResult Check(string username, DateTime utcNow);
}