# Security Matrix

این ماتریس مرجع امنیتی پروژه **BonyadRazi.Portal** است. هر API جدید قبل از انتشار باید در این ماتریس ثبت شود.

| Method | Path | Exposure | Auth | Policy | TenantScoped | RateLimit | ActionType |
|---|---|---|---|---|---|---|---|
| GET | /gateway/health | Public محدود | None/IP allowlist | - | No | - | HealthCheck |
| POST | /api/auth/login | Public | None | - | No | 10/min/IP | Login / LoginFailed |
| POST | /api/auth/refresh | Public | None | - | No | 20/min/IP | TokenRefresh |
| POST | /api/auth/revoke | Public | None | - | No | 20/min/IP | Logout / TokenRevoke |
| GET | /api/auth/me | Protected | JWT | Portal.Access | Yes | 120/min/IP | ViewProfile |
| GET | /api/users | Protected | JWT | Users.Read | Yes | 120/min/IP | ViewUsers |
| GET | /api/users/{userId} | Protected | JWT | Users.Read | Yes | 120/min/IP | ViewUser |
| POST | /api/users | Protected | JWT | Users.Manage | Yes | 120/min/IP | CreateUser |
| PUT | /api/users/{userId} | Protected | JWT | Users.Manage | Yes | 120/min/IP | UpdateUser |
| GET | /api/companies | Protected | JWT | Companies.Read | Yes | 120/min/IP | ViewCompany |
| GET | /api/companies/{companyCode} | Protected | JWT | Companies.Read | Yes | 120/min/IP | ViewCompany |
| PUT | /api/companies/{companyCode} | Protected | JWT | Companies.Manage | Yes | 120/min/IP | UpdateCompany |
| GET | /api/audit/denied | Protected | JWT | Audit.Read | Yes | 120/min/IP | ViewAuditLogs |
| GET | /health | Internal/Protected | JWT | System.Admin | No | 120/min/IP | ServiceHealth |

## قواعد اجرایی
- **Default Deny**: هر مسیری که در Gateway allowlist نشده باشد رد می‌شود.
- **JWT First**: APIهای protected بدون Bearer token معتبر نباید پاسخ دهند.
- **Tenant Isolation**: برای مسیرهای tenant-scoped، `company_code` فقط از claim خوانده می‌شود.
- **Audit**: ثبت `SecurityDenied` برای `401/403` و عدم ذخیره‌ی Token/Password در metadata.

## Test Coverage (CI / SecurityTests)
این بخش نشان می‌دهد چه سناریوهایی به صورت خودکار در CI تست می‌شوند.

### Companies (Tenant Isolation)
- **401**: بدون توکن → Unauthorized
- **403**: توکن معتبر + companyCode در route ≠ claim.company_code → Forbidden (cross-tenant)
- **200**: توکن معتبر + companyCode در route = claim.company_code → Ok (same-tenant)

### Audit Denied Report (Admin-only)
- **401**: بدون توکن → Unauthorized
- **403**: توکن معتبر با Role غیر Admin/SuperAdmin → Forbidden
- **200**: توکن معتبر با Role=Admin/SuperAdmin → Ok

## Incident Response
برای واکنش عملیاتی به رخدادهای امنیتی (افزایش 401/403، brute-force، cross-tenant)، به این سند مراجعه کنید:
- `docs/SecurityIncidentRunbook.md`