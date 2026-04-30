namespace BonyadRazi.Portal.Api.Security;

public sealed class UsernameLoginRateLimitOptions
{
    public bool Enabled { get; set; } = true;

    // تعداد مجاز تلاش login برای هر username در هر window
    public int PermitLimit { get; set; } = 20;

    // طول window بر حسب دقیقه
    public int WindowMinutes { get; set; } = 60;

    // پاکسازی رکوردهای قدیمی از حافظه
    public int CleanupIntervalMinutes { get; set; } = 10;
}