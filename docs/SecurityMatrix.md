# Security Matrix — BonyadRazi.Portal

این سند «مرجع اجرایی» امنیت است: هر Endpoint قبل از انتشار باید اینجا ثبت شود و تست‌های امنیتی (CI) آن را پوشش دهند.

## اصول حاکم
- **Default Deny**: هر مسیر API که صراحتاً Public نشده باشد، نیازمند JWT معتبر است.
- **JWT First**: تمام مسیرهای Protected فقط با Bearer Token معتبر.
- **Tenant Isolation**: Tenant key فقط از **Claim** (`company_code`) خوانده می‌شود؛ به body/query اعتماد نمی‌شود مگر کنترل‌شده.
- **Auditability**: رخدادهای امنیتی و عملیات‌های مهم با ActionType و Metadata ثبت می‌شوند.
- **No Secrets in Repo**: کلید JWT و ConnectionString ها فقط از ENV / Secret Store.

## Claims حداقلی JWT
- `sub` = UserId
- `company_code` = Tenant
- `jti` = TokenId
- `role`
- `iss`, `aud`, `exp`, `nbf`

## Policies پایه
- `Portal.Access`
- `Users.Read`, `Users.Manage`
- `Companies.Read`, `Companies.Manage`
- `Audit.Read`
- `System.Admin`

---

## Security Matrix (اجرایی)

> توضیح ستون‌ها:
> - Exposure: Public / Protected / Internal
> - Auth: None / JWT / IP allowlist
> - TenantScoped: آیا داده/عملیات باید به tenant کاربر محدود شود؟
> - RateLimit: در Gateway اعمال می‌شود
> - ActionType: نوع رخداد برای Audit Log

| Method | Path | Exposure | Auth | Policy | TenantScoped | RateLimit | ActionType |
|---|---|---|---|---|---:|---|---|
| GET | `/gateway/health` | Public محدود | IP allowlist | - | No | - | `HealthCheck` |
| POST | `/api/auth/login` | Public | None | - | No | 5/min/IP | `Login` / `LoginFailed` |
| POST | `/api/auth/refresh` | Public | None | - | No | 10/min/IP | `TokenRefresh` |
| POST | `/api/auth/revoke` | Public | None | - | No | 10/min/IP | `TokenRevoke` / `Logout` |
| GET | `/api/auth/me` | Protected | JWT | `Portal.Access` | Yes | 120/min/IP | `ViewProfile` |
| GET | `/api/users` | Protected | JWT | `Users.Read` | Yes | 120/min/IP | `ViewUsers` |
| GET | `/api/users/{userId}` | Protected | JWT | `Users.Read` | Yes | 120/min/IP | `ViewUser` |
| POST | `/api/users` | Protected | JWT | `Users.Manage` | Yes | 120/min/IP | `CreateUser` |
| PUT | `/api/users/{userId}` | Protected | JWT | `Users.Manage` | Yes | 120/min/IP | `UpdateUser` |
| GET | `/api/companies` | Protected | JWT | `Companies.Read` | Yes | 120/min/IP | `ViewCompany` |
| GET | `/api/companies/{companyCode:guid}` | Protected | JWT | `Companies.Read` | Yes | 120/min/IP | `ViewCompany` |
| PUT | `/api/companies/{companyCode:guid}` | Protected | JWT | `Companies.Manage` | Yes | 120/min/IP | `UpdateCompany` |
| GET | `/api/audit/actions` | Protected | JWT | `Audit.Read` | Yes | 120/min/IP | `ViewAuditLogs` |
| GET | `/api/audit/denied` | Protected (Admin-only) | JWT | `Audit.Read` (و Role/Policy ادمین) | Optional (filter) | 120/min/IP | `ViewAuditLogs` |
| GET | `/health` | Internal/Protected | JWT | `System.Admin` | No | 120/min/IP | `ServiceHealth` |

---

## SecurityDenied (401/403) — ثبت Audit واقعی
برای تمام 401/403 ها (از جمله:
- نبودن توکن
- Cross-Tenant
- Policy/Role Deny)

یک رکورد Audit با ActionType=`SecurityDenied` ثبت می‌شود.

حداقل Metadata پیشنهادی:
```json
{
  "statusCode": 403,
  "method": "GET",
  "path": "/api/companies/...",
  "remoteIp": "::1",
  "userAgent": "Mozilla/5.0 ...",
  "traceId": "...",
  "reason": "CrossTenant",
  "companyCode": "..."
}