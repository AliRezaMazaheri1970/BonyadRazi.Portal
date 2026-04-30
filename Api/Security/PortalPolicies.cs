using Microsoft.AspNetCore.Authorization;

namespace BonyadRazi.Portal.Api.Security;

public static class PortalPolicies
{
    public const string PortalAccess = "Portal.Access";

    public const string SystemAdmin = "System.Admin";

    public const string CompaniesRead = "Companies.Read";
    public const string CompaniesManage = "Companies.Manage";

    public const string UsersRead = "Users.Read";
    public const string UsersManage = "Users.Manage";

    public const string AuditRead = "Audit.Read";

    public const string ReportsRead = "Reports.Read";
    public const string ReportsDownload = "Reports.Download";

    public static void AddPortalPolicies(AuthorizationOptions options)
    {
        // System-level policies.
        // این policy نیاز به company_code ندارد، چون برای عملیات سیستمی و cross-tenant استفاده می‌شود.
        options.AddPolicy(SystemAdmin, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole("Admin", "SuperAdmin");
        });

        // General portal access.
        // فعلاً همه نقش‌های معتبر پرتال را قبول می‌کند.
        options.AddPolicy(PortalAccess, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(
                "User",
                "Customer",
                "CompanyAdmin",
                "Auditor",
                "Admin",
                "SuperAdmin");
        });

        // Tenant-scoped company read.
        // این همان منطق قبلی Program.cs است که حالا به جای درستش منتقل شده.
        options.AddPolicy(CompaniesRead, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim(PortalClaims.CompanyCode);
            policy.RequireRole(
                "User",
                "Customer",
                "CompanyAdmin",
                "Admin",
                "SuperAdmin");
        });

        // Company management.
        // برای مدیریت شرکت، claim شرکت لازم است و TenantConsistencyFilter روی route enforce می‌کند.
        options.AddPolicy(CompaniesManage, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim(PortalClaims.CompanyCode);
            policy.RequireRole(
                "CompanyAdmin",
                "Admin",
                "SuperAdmin");
        });

        // User read.
        options.AddPolicy(UsersRead, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim(PortalClaims.CompanyCode);
            policy.RequireRole(
                "CompanyAdmin",
                "Admin",
                "SuperAdmin");
        });

        // User management.
        options.AddPolicy(UsersManage, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim(PortalClaims.CompanyCode);
            policy.RequireRole(
                "CompanyAdmin",
                "Admin",
                "SuperAdmin");
        });

        // Audit read.
        // Auditor می‌تواند audit را بخواند، بدون اینکه الزاماً دسترسی company management داشته باشد.
        options.AddPolicy(AuditRead, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(
                "Auditor",
                "GlobalAuditor",
                "Admin",
                "SuperAdmin");
        });

        // Reports read.
        options.AddPolicy(ReportsRead, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim(PortalClaims.CompanyCode);
            policy.RequireRole(
                "User",
                "Customer",
                "CompanyAdmin",
                "Admin",
                "SuperAdmin");
        });

        // Reports download.
        options.AddPolicy(ReportsDownload, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim(PortalClaims.CompanyCode);
            policy.RequireRole(
                "User",
                "Customer",
                "CompanyAdmin",
                "Admin",
                "SuperAdmin");
        });
    }
}