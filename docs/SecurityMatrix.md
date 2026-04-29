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
