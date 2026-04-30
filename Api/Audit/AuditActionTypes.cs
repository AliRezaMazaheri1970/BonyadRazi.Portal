namespace BonyadRazi.Portal.Api.Audit;

public static class AuditActionTypes
{
    // Auth
    public const string AuthLoginSucceeded = "Auth.LoginSucceeded";
    public const string AuthLoginFailed = "Auth.LoginFailed";
    public const string AuthRefreshSucceeded = "Auth.RefreshSucceeded";
    public const string AuthRefreshFailed = "Auth.RefreshFailed";
    public const string AuthRefreshReuseDetected = "Auth.RefreshReuseDetected";
    public const string AuthTokenRevoked = "Auth.TokenRevoked";
    public const string AuthLogout = "Auth.Logout";
    public const string AuthLogoutAll = "Auth.LogoutAll";
    public const string AuthPasswordChanged = "Auth.PasswordChanged";

    // Users
    public const string UsersViewed = "Users.Viewed";
    public const string UsersCreated = "Users.Created";
    public const string UsersUpdated = "Users.Updated";
    public const string UsersStatusChanged = "Users.StatusChanged";
    public const string UsersPasswordReset = "Users.PasswordReset";
    public const string UsersRoleChanged = "Users.RoleChanged";
    public const string UsersUnlocked = "Users.Unlocked";

    // Companies
    public const string CompaniesViewed = "Companies.Viewed";
    public const string CompaniesSettingsUpdated = "Companies.SettingsUpdated";
    public const string CompaniesDirectorySearched = "Companies.DirectorySearched";

    // Reports
    public const string ReportsInvoicesViewed = "Reports.InvoicesViewed";
    public const string ReportsInvoiceViewed = "Reports.InvoiceViewed";
    public const string ReportsInvoicePdfDownloaded = "Reports.InvoicePdfDownloaded";
    public const string ReportsContractsViewed = "Reports.ContractsViewed";
    public const string ReportsContractViewed = "Reports.ContractViewed";
    public const string ReportsContractDocumentDownloaded = "Reports.ContractDocumentDownloaded";
    public const string ReportsWorkflowViewed = "Reports.WorkflowViewed";

    // Security
    public const string SecurityAccessDenied = "Security.AccessDenied";
    public const string SecurityTenantViolation = "Security.TenantViolation";
    public const string SecurityIpBlocked = "Security.IpBlocked";
    public const string SecurityUnknownApiPathBlocked = "Security.UnknownApiPathBlocked";
    public const string SecurityLoginRateLimited = "Security.LoginRateLimited";

    // System
    public const string SystemHealthChecked = "System.HealthChecked";
}