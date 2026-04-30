namespace BonyadRazi.Portal.Api.Audit;

public static class AuditRedaction
{
    public const string RedactedValue = "[REDACTED]";

    private static readonly string[] SensitiveKeys =
    {
        "password",
        "currentPassword",
        "newPassword",
        "token",
        "accessToken",
        "refreshToken",
        "access_token",
        "refresh_token",
        "authorization",
        "bearer",
        "cookie",
        "set-cookie",
        "secret",
        "connectionString",
        "client_secret",
        "api_key"
    };

    public static bool IsSensitiveKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return SensitiveKeys.Any(x =>
            key.Contains(x, StringComparison.OrdinalIgnoreCase));
    }

    public static string RedactIfSensitive(string key, string? value)
    {
        if (IsSensitiveKey(key))
        {
            return RedactedValue;
        }

        return value ?? string.Empty;
    }
}