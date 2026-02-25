using Microsoft.AspNetCore.Authorization;

namespace BonyadRazi.Portal.Api.Security;

public static class PortalPolicies
{
    public const string PortalAccess = "Portal.Access";
    public const string CompaniesRead = "Companies.Read";
    public const string CompaniesManage = "Companies.Manage";
    public const string SystemAdmin = "System.Admin";
    public const string UsersRead = "Users.Read";
    public const string UsersManage = "Users.Manage";
    public const string AuditRead = "Audit.Read";

    public static void AddPortalPolicies(AuthorizationOptions options)
    {
        // اگر SuperAdmin هم دارید، همینجا اضافه کنید
        options.AddPolicy(SystemAdmin, p => p.RequireRole("Admin", "SuperAdmin"));

        // فعلاً همه Admin-only (امن و ساده). بعداً می‌تونی Claim-based کنی.
        options.AddPolicy(PortalAccess, p => p.RequireRole("Admin", "SuperAdmin"));
        options.AddPolicy(CompaniesRead, p => p.RequireRole("Admin", "SuperAdmin"));
        options.AddPolicy(CompaniesManage, p => p.RequireRole("Admin", "SuperAdmin"));
        options.AddPolicy(UsersRead, p => p.RequireRole("Admin", "SuperAdmin"));
        options.AddPolicy(UsersManage, p => p.RequireRole("Admin", "SuperAdmin"));
        options.AddPolicy(AuditRead, p => p.RequireRole("Admin", "SuperAdmin"));
    }
}