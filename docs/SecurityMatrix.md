# BonyadRazi Portal - Security Matrix

## هدف سند

این سند مشخص می‌کند هر endpoint پروژه چه سطح امنیتی، policy، tenant check و audit نیاز دارد.

هر API جدید باید قبل یا همزمان با پیاده‌سازی در این سند ثبت شود تا امنیت پروژه مرحله‌به‌مرحله قابل کنترل، تست‌پذیر و قابل مرور باشد.

---

## وضعیت فعلی زیرساخت امنیتی

معماری فعلی پروژه:

```text
Client
  ↓
HAProxy
  ↓
Gateway / YARP
  ↓
API
```

اصول فعلی:

```text
HAProxy:
- فقط Host معتبر را قبول می‌کند
- درخواست مستقیم با IP را رد می‌کند
- X-Forwarded-* را خودش تنظیم می‌کند
- اطلاعات forwarded جعلی از سمت client نباید قابل اعتماد باشد

Gateway:
- فقط از HAProxy قابل دسترسی است
- API allowlist دارد
- IP allowlist دارد
- JWT را قبل از proxy کردن validate می‌کند
- مسیرهای ناشناخته API را reject می‌کند

API:
- فقط روی loopback اجرا می‌شود
- خودش دوباره JWT و Policy را enforce می‌کند
- ForwardedHeaders را قبل از Audit و Authorization پردازش می‌کند
- Tenant Isolation را داخل business endpointها enforce می‌کند
- SecurityDenied و عملیات حساس را Audit می‌کند
```

---

## اصول امنیتی ثابت

این اصول نباید در فازهای بعدی تغییر کنند:

```text
1. API مستقیم در شبکه expose نمی‌شود.
2. فقط Gateway اجازه دسترسی به API را دارد.
3. Gateway قبل از proxy کردن، مسیر را با ApiAllowPrefixes بررسی می‌کند.
4. Gateway برای مسیرهای محافظت‌شده JWT را validate می‌کند.
5. API دوباره JWT، Policy و Tenant Check را validate می‌کند.
6. Default behavior باید deny باشد.
7. هر endpoint جدید باید تست 401 و 403 داشته باشد.
8. هر عملیات حساس باید Audit شود.
9. اطلاعات حساس نباید در Audit ذخیره شوند.
10. companyCode برای کاربران tenant-scoped باید از JWT خوانده شود، نه از ورودی کاربر.
```

---

## Gateway Allow Prefixes

در وضعیت فعلی، prefixهای مجاز Gateway:

```json
[
  "/api/auth",
  "/api/users",
  "/api/companies",
  "/api/audit",
  "/api/diagnostics",
  "/health",
  "/gateway/health"
]
```

در فاز Reports باید این prefix اضافه شود:

```json
"/api/reports"
```

اگر در آینده Documents به صورت جدا از Reports طراحی شد، این prefix نیز باید بررسی و اضافه شود:

```json
"/api/documents"
```

---

## Current Security Matrix

| Endpoint | JWT | Policy | Tenant Check | Audit | Status |
|---|---:|---|---:|---:|---|
| `GET /health` | No | Anonymous | No | No | Active |
| `GET /gateway/health` | No | Anonymous | No | No | Active |
| `POST /api/auth/login` | No | PublicAuth | No | Yes | Existing |
| `POST /api/auth/refresh` | No / RefreshToken | PublicAuth | No | Yes | Existing |
| `POST /api/auth/revoke` | Depends | Authenticated or RefreshToken | No | Yes | Existing |
| `GET /api/auth/me` | Yes | Authenticated | No | Optional | Planned |
| `POST /api/auth/logout` | Yes | Authenticated | No | Yes | Planned |
| `POST /api/auth/logout-all` | Yes | Authenticated | No | Yes | Planned |
| `POST /api/account/change-password` | Yes | Authenticated | No | Yes | Planned |
| `GET /api/diagnostics/auth-test` | Yes | Authenticated | No | No | Active |
| `GET /api/diagnostics/admin-test` | Yes | SystemAdmin | No | No | Active |
| `GET /api/diagnostics/tenant-test/{companyCode}` | Yes | Authenticated | Yes | No | Active |
| `GET /api/users` | Yes | Admin / CompanyAdmin | Yes | Yes | Planned |
| `GET /api/users/{userId}` | Yes | Admin / CompanyAdmin | Yes | Yes | Planned |
| `POST /api/users` | Yes | Admin / CompanyAdmin | Yes | Yes | Planned |
| `PUT /api/users/{userId}` | Yes | Admin / CompanyAdmin | Yes | Yes | Planned |
| `PATCH /api/users/{userId}/status` | Yes | Admin / CompanyAdmin | Yes | Yes | Planned |
| `POST /api/users/{userId}/reset-password` | Yes | Admin / CompanyAdmin | Yes | Yes | Planned |
| `POST /api/users/{userId}/unlock` | Yes | Admin / CompanyAdmin | Yes | Yes | Planned |
| `GET /api/companies/me` | Yes | Authenticated | Yes | Optional | Planned |
| `GET /api/companies/{companyCode}` | Yes | Authenticated / Admin | Yes | Yes | Existing / Planned |
| `GET /api/companies/directory` | Yes | Admin | No | Yes | Planned |
| `PUT /api/companies/{companyCode}/settings` | Yes | Admin | Yes | Yes | Planned |
| `GET /api/companies/{companyCode}/users` | Yes | Admin / CompanyAdmin | Yes | Yes | Planned |
| `GET /api/reports/company/invoices` | Yes | Customer / Admin | Yes | Yes | Planned |
| `GET /api/reports/company/invoices/{masterBillCode}` | Yes | Customer / Admin | Yes | Yes | Planned |
| `GET /api/reports/company/invoices/{masterBillCode}/pdf` | Yes | Customer / Admin | Yes | Yes | Planned |
| `GET /api/reports/company/contracts` | Yes | Customer / Admin | Yes | Yes | Planned |
| `GET /api/reports/company/contracts/{contractCode}` | Yes | Customer / Admin | Yes | Yes | Planned |
| `GET /api/reports/company/contracts/{contractCode}/document` | Yes | Customer / Admin | Yes | Yes | Planned |
| `GET /api/reports/company/workflow` | Yes | Customer / Admin | Yes | Yes | Planned |
| `GET /api/reports/company/workflow/{workflowId}` | Yes | Customer / Admin | Yes | Yes | Planned |
| `GET /api/audit/actions` | Yes | Admin / Auditor | No | Optional | Planned |
| `GET /api/audit/logs` | Yes | Admin / Auditor | Optional | Yes | Planned |
| `GET /api/audit/denied` | Yes | Admin / Auditor | Optional | Yes | Existing |
| `GET /api/audit/users/{userId}` | Yes | Admin / Auditor | Optional | Yes | Planned |
| `GET /api/audit/companies/{companyCode}` | Yes | Admin / Auditor / CompanyAdmin | Yes | Yes | Planned |

---

## Endpoint Status Definition

| Status | Meaning |
|---|---|
| Active | پیاده‌سازی شده، تست دارد و در حال استفاده است |
| Existing | در کد وجود دارد، اما ممکن است نیاز به refactor یا تکمیل تست داشته باشد |
| Planned | در نقشه‌راه API است و هنوز پیاده‌سازی نشده |
| Deprecated | فعلاً وجود دارد، اما باید حذف یا جایگزین شود |

---

## JWT Claims Required

حداقل claimهای لازم در JWT:

| Claim | Required | Description |
|---|---:|---|
| `sub` | Yes | شناسه کاربر |
| `role` | Yes | نقش یا نقش‌های کاربر |
| `company_code` | For tenant users | شناسه شرکت کاربر |
| `jti` | Recommended | شناسه یکتای access token |
| `name` | Optional | نام نمایشی یا username |

برای endpointهای tenant-scoped، نبودن claim شرکت باید منجر به `403 Forbidden` شود، نه دسترسی آزاد.

---

## Role Model

نقش‌های پیشنهادی پروژه:

| Role | Description |
|---|---|
| `SuperAdmin` | دسترسی کامل مدیریتی و امنیتی |
| `Admin` | مدیریت عملیاتی سیستم |
| `Auditor` | دسترسی read-only به audit |
| `CompanyAdmin` | مدیریت کاربران و اطلاعات شرکت خودش |
| `Customer` | کاربر عادی شرکت |
| `User` | نقش عمومی یا تستی، در صورت نیاز |

نکته: اگر در کد فعلی نام نقش‌ها متفاوت است، این جدول باید با `PortalPolicies` و تست‌های واقعی هماهنگ شود.

---

## Policy Model

Policyهای پیشنهادی:

| Policy | Roles / Rule |
|---|---|
| `Authenticated` | هر کاربر دارای JWT معتبر |
| `SystemAdmin` | `Admin` یا `SuperAdmin` |
| `Auditor` | `Auditor` یا `Admin` یا `SuperAdmin` |
| `CompanyAdmin` | `CompanyAdmin` محدود به شرکت خودش |
| `CustomerAccess` | `Customer` یا `CompanyAdmin` محدود به شرکت خودش |
| `ReportsAccess` | `Customer` یا `CompanyAdmin` یا `Admin` با tenant rule |
| `AuditRead` | `Auditor` یا `Admin` یا `SuperAdmin` |

---

## Tenant Isolation Rules

قواعد ثابت tenant isolation:

```text
1. برای Customer، شرکت از JWT خوانده می‌شود.
2. برای CompanyAdmin، شرکت از JWT خوانده می‌شود.
3. کاربر عادی نباید بتواند companyCode شرکت دیگر را در route یا query بفرستد.
4. Admin و SuperAdmin می‌توانند با policy مشخص روی شرکت‌های مختلف کار کنند.
5. هر tenant violation باید 403 Forbidden بدهد.
6. tenant violation در endpointهای حساس باید Audit شود.
```

الگوی درست:

```text
Customer:
companyCode = User claim company_code

Admin:
companyCode می‌تواند از route یا query دریافت شود، اما فقط با policy مناسب
```

الگوی غلط:

```text
Customer:
GET /api/reports/company/invoices?companyCode=...
```

برای کاربران tenant-scoped، `companyCode` نباید از query string به‌عنوان منبع اعتماد استفاده شود.

---

## Audit Requirements

عملیات زیر باید Audit شوند:

```text
Auth.LoginSucceeded
Auth.LoginFailed
Auth.RefreshSucceeded
Auth.RefreshFailed
Auth.TokenRevoked
Auth.Logout
Auth.LogoutAll
Auth.PasswordChanged

Users.Created
Users.Updated
Users.StatusChanged
Users.PasswordReset
Users.RoleChanged
Users.Unlocked

Companies.Viewed
Companies.SettingsUpdated
Companies.DirectorySearched

Reports.InvoicesViewed
Reports.InvoicePdfDownloaded
Reports.ContractsViewed
Reports.ContractDocumentDownloaded
Reports.WorkflowViewed

Security.AccessDenied
Security.TenantViolation
Security.IpBlocked
Security.UnknownApiPathBlocked

System.HealthChecked
```

نکته: `System.HealthChecked` فعلاً ضروری نیست و فقط در صورت نیاز عملیاتی فعال شود تا لاگ‌ها شلوغ نشوند.

---

## Audit Redaction Policy

اطلاعات زیر نباید در Audit ذخیره شوند:

```text
password
token
access_token
refresh_token
Authorization
Bearer
cookie
set-cookie
secret
connectionString
client_secret
api_key
```

اگر query string شامل اطلاعات حساس باشد، مقدار آن باید به شکل زیر ثبت شود:

```text
[REDACTED]
```

نمونه درست:

```json
{
  "queryString": "[REDACTED]"
}
```

نمونه غلط:

```json
{
  "queryString": "?access_token=abc123&password=test"
}
```

---

## Required Tests Per Protected Endpoint

برای هر endpoint محافظت‌شده:

| Test | Expected |
|---|---|
| Without JWT | 401 |
| Invalid JWT | 401 |
| Expired JWT | 401 |
| Valid JWT but wrong role | 403 |
| Valid JWT but wrong tenant | 403 |
| Valid JWT and valid access | 200 / 201 / 204 |

برای هر endpoint عمومی:

| Test | Expected |
|---|---|
| Valid request | 200 / 400 قابل انتظار |
| Sensitive input should not be logged | Redacted |
| Unexpected method | 405 یا 404 طبق routing |

---

## Gateway Security Rules

هر prefix جدید باید در این سه محل بررسی شود:

```text
1. Gateway/appsettings.json
2. Gateway/appsettings.Production.json
3. Tests/BonyadRazi.Portal.GatewayTests/GatewayFactory.cs
```

اگر prefix جدید به `ApiAllowPrefixes` اضافه نشود، Gateway باید آن را به عنوان unknown API path رد کند.

---

## API Security Rules

هر Controller جدید باید این موارد را رعایت کند:

```text
1. [ApiController]
2. route استاندارد زیر /api
3. [Authorize] یا policy مشخص برای endpointهای غیرعمومی
4. tenant check برای داده‌های مربوط به شرکت
5. عدم اعتماد به companyCode ورودی برای کاربران tenant-scoped
6. عدم برگشت اطلاعات حساس
7. ثبت Audit برای عملیات حساس
```

---

## HTTP Status Rules

| Status | Usage |
|---|---|
| 200 OK | دریافت یا عملیات موفق |
| 201 Created | ایجاد منبع جدید |
| 204 No Content | عملیات موفق بدون body |
| 400 Bad Request | validation error یا request ناقص |
| 401 Unauthorized | JWT وجود ندارد، نامعتبر است یا expire شده |
| 403 Forbidden | کاربر احراز هویت شده ولی دسترسی ندارد یا tenant violation |
| 404 Not Found | مسیر یا داده پیدا نشد |
| 409 Conflict | تداخل business، مثل username تکراری |
| 423 Locked | حساب کاربری lock شده |
| 500 Internal Server Error | خطای غیرمنتظره |

---

## Security Definition of Done

هر API جدید فقط وقتی پذیرفته می‌شود که:

```text
1. در ApiContract.md ثبت شده باشد.
2. در SecurityMatrix.md ثبت شده باشد.
3. در Gateway allowlist بررسی شده باشد.
4. JWT behavior مشخص باشد.
5. Policy مشخص داشته باشد.
6. Tenant isolation در صورت نیاز پیاده شده باشد.
7. Audit در صورت حساس بودن operation پیاده شده باشد.
8. تست‌های 401 و 403 داشته باشد.
9. تست موفق 200/201/204 داشته باشد.
10. اگر endpoint tenant-scoped است، تست cross-tenant داشته باشد.
11. اگر داده حساس دارد، redaction تست شده باشد.
```

---

## Change Log

| Date | Change |
|---|---|
| 2026-04-29 | اضافه شدن Health و Diagnostics به Security Matrix |

---

# Security Foundation Update - 2026-04-30

## وضعیت نهایی Security Foundation

آخرین وضعیت تثبیت‌شده:

```text
Tests: 27
Failed: 0
Succeeded: 27
```

موارد تکمیل‌شده:

```text
✅ X-Forwarded-For spoofing بسته شد
✅ RequestAuditMetadataFactory امن شد
✅ TenantConsistencyFilter اضافه شد
✅ RequireTenantMatch اضافه شد
✅ Security.TenantViolation audit شد
✅ RefreshTokenCleanupHostedService برای تست‌ها اصلاح شد
✅ Refresh Token Reuse Detection اضافه شد
✅ Auth.RefreshReuseDetected audit شد
✅ Authorization policies متمرکز شدند
✅ Username-based login rate limit اضافه شد
✅ Security.LoginRateLimited audit شد
✅ DevSeed hardening انجام شد
✅ AuditActionTypes / AuditMetadataKeys / AuditRedaction اضافه شد
✅ Security.AccessDenied استاندارد شد
```

---

## اصول امنیتی تکمیل‌شده

اصول زیر از این مرحله به بعد بخشی از baseline امنیتی پروژه هستند:

```text
1. API مستقیم در شبکه expose نمی‌شود.
2. فقط Gateway اجازه دسترسی به API را دارد.
3. Gateway قبل از proxy کردن، مسیر را با ApiAllowPrefixes بررسی می‌کند.
4. Gateway برای مسیرهای محافظت‌شده JWT را validate می‌کند.
5. API دوباره JWT، Policy و Tenant Check را validate می‌کند.
6. Default behavior باید deny باشد.
7. endpointهای tenant-scoped باید از [RequireTenantMatch] استفاده کنند.
8. tenant isolation داخل API با TenantConsistencyFilter enforce می‌شود.
9. هر tenant violation باید 403 Forbidden بدهد.
10. هر tenant violation باید با action type Security.TenantViolation audit شود.
11. login علاوه بر rate limit شبکه/Gateway، rate limit مبتنی بر username هم دارد.
12. refresh token reuse باید باعث revoke شدن refresh tokenهای فعال همان کاربر شود.
13. DevSeed فقط با environment variable صریح فعال می‌شود.
14. ActionTypeهای audit باید از AuditActionTypes خوانده شوند.
15. اطلاعات حساس نباید در Audit ذخیره شوند.
```

---

## Implemented Security Audit Events

رخدادهای امنیتی زیر پیاده‌سازی و با تست پوشش داده شده‌اند:

| Action Type | Trigger | Status | Test Coverage |
|---|---|---|---|
| `Security.AccessDenied` | پاسخ‌های 401/403 توسط `SecurityDeniedAuditMiddleware` | Active | Middleware behavior + audit endpoint alignment |
| `Security.LoginRateLimited` | محدود شدن login برای یک username | Active | `Login_SameUsernameTooManyAttempts_ShouldWriteAuditLog` |
| `Auth.RefreshReuseDetected` | استفاده دوباره از refresh token revoke/rotate شده | Active | `Refresh_ReusingRotatedRefreshToken_ShouldWriteAuditLog` |
| `Security.TenantViolation` | mismatch بین `companyCode` route و claim کاربر | Active | `Companies_Get_CrossTenant_ShouldWriteTenantViolationAuditLog` |

---

## Updated Endpoint Security Matrix

| Endpoint | JWT | Policy | Tenant Check | Audit | Status |
|---|---:|---|---:|---:|---|
| `POST /api/auth/login` | No | PublicAuth | No | Yes: `Security.LoginRateLimited` implemented; `Auth.LoginSucceeded` / `Auth.LoginFailed` planned | Existing |
| `POST /api/auth/refresh` | No / RefreshToken | PublicAuth | No | Yes: `Auth.RefreshReuseDetected` implemented; `Auth.RefreshSucceeded` / `Auth.RefreshFailed` planned | Existing |
| `POST /api/auth/revoke` | Depends | Authenticated or RefreshToken | No | Yes: `Auth.TokenRevoked` planned | Existing |
| `GET /api/diagnostics/tenant-test/{companyCode}` | Yes | Authenticated | Yes: `[RequireTenantMatch]` | Yes: `Security.TenantViolation` on mismatch | Active |
| `GET /api/companies/{companyCode}` | Yes | `Companies.Read` | Yes: `[RequireTenantMatch]` | Yes: `Security.TenantViolation` on mismatch | Active |
| `GET /api/audit/denied` | Yes | `Audit.Read` | Optional | Yes: reads `Security.AccessDenied` events | Existing |

---

## Policy Model - Current Baseline

| Policy | Roles / Rule |
|---|---|
| `Portal.Access` | نقش‌های معتبر پرتال |
| `System.Admin` | `Admin` یا `SuperAdmin` |
| `Companies.Read` | کاربر احراز هویت‌شده دارای `company_code` و نقش مجاز |
| `Companies.Manage` | `CompanyAdmin` یا `Admin` یا `SuperAdmin` با کنترل tenant |
| `Users.Read` | `CompanyAdmin` یا `Admin` یا `SuperAdmin` |
| `Users.Manage` | `CompanyAdmin` یا `Admin` یا `SuperAdmin` |
| `Audit.Read` | `Auditor` یا `Admin` یا `SuperAdmin` |
| `Reports.Read` | کاربر مجاز دارای `company_code` |
| `Reports.Download` | کاربر مجاز دارای `company_code` |

---

## Tenant Isolation Rules - Updated

قواعد ثابت tenant isolation:

```text
1. برای Customer، شرکت از JWT خوانده می‌شود.
2. برای CompanyAdmin، شرکت از JWT خوانده می‌شود.
3. کاربر عادی نباید بتواند companyCode شرکت دیگر را در route یا query بفرستد.
4. مقدار route مثل companyCode باید با claim شرکت کاربر برابر باشد.
5. نبودن claim شرکت یا نامعتبر بودن آن باید 403 Forbidden بدهد.
6. mismatch بین route و claim باید 403 Forbidden بدهد.
7. tenant violation باید با action type Security.TenantViolation audit شود.
8. حالت پیش‌فرض strict است.
```

برای endpointهای tenant-scoped باید attribute زیر استفاده شود:

```csharp
[RequireTenantMatch]
```

برای endpointهایی که واقعاً نیاز به عبور system role دارند، فقط با تصمیم صریح از این حالت استفاده شود:

```csharp
[RequireTenantMatch(AllowSystemRoles = true)]
```

---

## Refresh Token Security

قواعد refresh token:

```text
1. refresh token خام هرگز در دیتابیس ذخیره نمی‌شود.
2. فقط hash شده refresh token ذخیره می‌شود.
3. refresh token در هر refresh موفق rotate می‌شود.
4. token قبلی بعد از refresh موفق با reason = rotated revoke می‌شود.
5. اگر refresh token قبلاً revoke/rotate شده دوباره استفاده شود، reuse detection فعال می‌شود.
6. در reuse detection، پاسخ 401 Unauthorized برمی‌گردد.
7. در reuse detection، همه refresh tokenهای فعال همان user revoke می‌شوند.
8. reason برابر reuse_detected ثبت می‌شود.
9. audit event برابر Auth.RefreshReuseDetected ثبت می‌شود.
```

تست‌های پوشش‌دهنده:

```text
Refresh_ReusingRotatedRefreshToken_ShouldRevokeActiveTokensForUser
Refresh_ReusingRotatedRefreshToken_ShouldWriteAuditLog
```

---

## Login Rate Limit

لایه‌های rate limit فعلی:

| Layer | Key | Purpose |
|---|---|---|
| Gateway | IP | کاهش فشار و abuse از یک source |
| API | Username | جلوگیری از حمله توزیع‌شده روی یک حساب خاص |

قواعد username-based login rate limit:

- تنظیمات از مسیر زیر خوانده می‌شود:

```text
Security:UsernameLoginRateLimit
```

- مقدارهای فعلی:

```json
{
  "Enabled": true,
  "PermitLimit": 20,
  "WindowMinutes": 60,
  "CleanupIntervalMinutes": 10
}
```

- اگر limit رد شود:
  - پاسخ `429 Too Many Requests` برمی‌گردد.
  - header زیر تنظیم می‌شود:

```http
Retry-After: <seconds>
```

  - audit event زیر ثبت می‌شود:

```text
Security.LoginRateLimited
```

تست‌های پوشش‌دهنده:

```text
Login_SameUsernameTooManyAttempts_ShouldReturn429
Login_SameUsernameTooManyAttempts_ShouldWriteAuditLog
```

---

## DevSeed Security

DevSeed فقط برای Development است و نباید صرفاً با `ASPNETCORE_ENVIRONMENT=Development` فعال شود.

برای فعال شدن DevSeed باید environment variable زیر صریحاً تنظیم شود:

```text
BONYADRAZI_DEV_SEED_ALLOW=true
```

همچنین password باید از environment variable زیر بیاید و حداقل ۱۲ کاراکتر باشد:

```text
BONYADRAZI_DEV_SEED_PASSWORD
```

رمز hardcoded مثل `admin/admin` مجاز نیست.

در Production این environment variableها نباید تنظیم شوند.

---

## Updated Audit Requirements

عملیات زیر باید Audit شوند:

```text
Auth.LoginSucceeded
Auth.LoginFailed
Auth.RefreshSucceeded
Auth.RefreshFailed
Auth.RefreshReuseDetected
Auth.TokenRevoked
Auth.Logout
Auth.LogoutAll
Auth.PasswordChanged

Users.Created
Users.Updated
Users.StatusChanged
Users.PasswordReset
Users.RoleChanged
Users.Unlocked

Companies.Viewed
Companies.SettingsUpdated
Companies.DirectorySearched

Reports.InvoicesViewed
Reports.InvoiceViewed
Reports.InvoicePdfDownloaded
Reports.ContractsViewed
Reports.ContractViewed
Reports.ContractDocumentDownloaded
Reports.WorkflowViewed

Security.AccessDenied
Security.TenantViolation
Security.IpBlocked
Security.UnknownApiPathBlocked
Security.LoginRateLimited
```

---

## Current Test Coverage

وضعیت فعلی تست‌ها بعد از Security Foundation:

```text
Total tests: 27
Failed: 0
Succeeded: 27
```

پوشش‌های مهم امنیتی:

```text
- Gateway diagnostics JWT enforcement
- API diagnostics auth and tenant checks
- Header spoofing regression
- RequestAuditMetadataFactory IP handling
- Refresh token cleanup with EF InMemory
- Refresh token reuse detection
- Refresh reuse audit
- Username login rate limit
- Login rate limit audit
- Tenant violation audit
- Security access denied audit standardization
```

---

## Updated Security Definition of Done

هر API جدید فقط وقتی پذیرفته می‌شود که:

```text
1. در ApiContract.md ثبت شده باشد.
2. در SecurityMatrix.md ثبت شده باشد.
3. در Gateway allowlist بررسی شده باشد.
4. JWT behavior مشخص باشد.
5. Policy مشخص داشته باشد.
6. اگر tenant-scoped است، [RequireTenantMatch] داشته باشد.
7. اگر tenant violation ممکن است، audit event Security.TenantViolation ثبت شود.
8. اگر operation حساس است، audit event مشخص داشته باشد.
9. اطلاعات حساس در audit redacted شوند.
10. تست‌های 401 و 403 داشته باشد.
11. تست موفق 200/201/204 داشته باشد.
12. اگر endpoint tenant-scoped است، تست cross-tenant داشته باشد.
13. اگر audit لازم دارد، تست ثبت audit داشته باشد.
14. اگر داده حساس دارد، redaction تست شده باشد.
```

---

## Change Log Update

| Date | Change |
|---|---|
| 2026-04-30 | تکمیل Security Foundation، AuditActionTypes، rate limit، refresh reuse detection و tenant violation audit |
