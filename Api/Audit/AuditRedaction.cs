namespace BonyadRazi.Portal.Api.Audit;

public static class AuditRedaction
{
    public const string RedactedValue = "[REDACTED]";

    private static readonly string[] SensitiveKeys =
    {
        "password",
        "pass",
        "pwd",
        "currentPassword",
        "newPassword",

        "token",
        "accessToken",
        "refreshToken",
        "access_token",
        "refresh_token",
        "jwt",

        "authorization",
        "bearer",
        "cookie",
        "set-cookie",

        "secret",
        "client_secret",

        "api_key",
        "apikey",
        "key",

        "connectionString",
        "connectionstring",
        "connection_string"
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

    public static bool ContainsSensitiveKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return SensitiveKeys.Any(x =>
            value.Contains(x, StringComparison.OrdinalIgnoreCase));
    }

    public static string RedactIfSensitive(string key, string? value)
    {
        if (IsSensitiveKey(key))
        {
            return RedactedValue;
        }

        return value ?? string.Empty;
    }

    public static string RedactTextIfContainsSensitiveKey(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        value = value.Trim();

        if (ContainsSensitiveKey(value))
        {
            return RedactedValue;
        }

        return value.Length <= maxLen
            ? value
            : value[..maxLen];
    }
}
