# BonyadRazi Portal - API Contract

## هدف سند

این سند قرارداد فعلی API پروژه BonyadRazi Portal را ثبت می‌کند.

هر endpoint جدید باید قبل یا همزمان با پیاده‌سازی در این سند اضافه شود تا WebApp، Gateway، تست‌ها و Runbook بر اساس یک قرارداد مشخص جلو بروند.

---

## اصول کلی API

- تمام endpointهای `/api/*` از مسیر Gateway در دسترس هستند.
- API در Production فقط روی `127.0.0.1:6001` اجرا می‌شود.
- Gateway روی `192.168.0.103:6000` اجرا می‌شود.
- HAProxy تنها ورودی شبکه داخلی است.
- همه endpointهای محافظت‌شده باید JWT معتبر داشته باشند.
- API علاوه بر Gateway، خودش هم JWT و Policy را enforce می‌کند.
- tenant-scoped endpointها باید `company_code` را از JWT بخوانند، نه از ورودی کاربر.
- عملیات حساس باید Audit شوند.
- اطلاعات حساس مثل password، token و Authorization header نباید در Audit ذخیره شوند.

---

## Health

### GET /health

وضعیت پایه API را برمی‌گرداند.

#### Authentication

Anonymous

#### Response 200

```json
{
  "status": "ok",
  "where": "api",
  "utc": "2026-04-29T12:00:00Z"
}

---

# API Contract Update - Security Foundation 2026-04-30

## وضعیت فعلی قرارداد

| مورد | مقدار |
|---|---|
| Project | BonyadRazi.Portal |
| Scope | API only |
| Current Phase | Security Foundation completed / API Expansion next |
| Current Tests | 27 passed, 0 failed |
| Gateway | YARP |
| API Production Address | `http://127.0.0.1:6001` |
| Gateway Production Address | `http://192.168.0.103:6000` |
| Public/Internal DNS | `customers.razi-foundation.com` |

---

## Authentication Security Behavior

### Login rate limit

`POST /api/auth/login` علاوه بر مکانیزم lockout داخلی کاربر، دارای username-based rate limit است.

اگر تعداد تلاش برای یک username از limit عبور کند:

| Status | Meaning |
|---|---|
| `429 Too Many Requests` | تلاش بیش از حد برای یک username |

Header:

```http
Retry-After: <seconds>
```

Audit:

```text
Security.LoginRateLimited
```

### Refresh token reuse detection

اگر refresh token قبلاً revoke/rotate شده دوباره استفاده شود:

| Behavior | Result |
|---|---|
| Response | `401 Unauthorized` |
| Active refresh tokens for user | revoked |
| Revoke reason | `reuse_detected` |
| Audit action | `Auth.RefreshReuseDetected` |

---

## Updated Auth Contract

### POST `/api/auth/login`

#### Request

```json
{
  "username": "customer1",
  "password": "P@ssw0rd!"
}
```

#### Response 200

```json
{
  "access_token": "<jwt>",
  "expires_in": 900,
  "refresh_token": "<refresh-token>",
  "refresh_expires_in": 2592000
}
```

#### Responses

| Status | Meaning |
|---|---|
| `200 OK` | login موفق |
| `400 Bad Request` | body ناقص یا نامعتبر |
| `401 Unauthorized` | username یا password اشتباه |
| `403 Forbidden` | کاربر غیرفعال است |
| `423 Locked` | حساب lock شده است |
| `429 Too Many Requests` | تعداد تلاش login برای username بیش از حد مجاز است |

#### Audit

| Action | When |
|---|---|
| `Auth.LoginSucceeded` | ورود موفق - planned |
| `Auth.LoginFailed` | ورود ناموفق - planned |
| `Security.LoginRateLimited` | محدود شدن تلاش login برای username - implemented |

---

### POST `/api/auth/refresh`

#### Request

```json
{
  "refresh_token": "<refresh-token>"
}
```

#### Response 200

```json
{
  "access_token": "<jwt>",
  "expires_in": 900,
  "refresh_token": "<new-refresh-token>",
  "refresh_expires_in": 2592000
}
```

#### Rules

- refresh token خام نباید در دیتابیس ذخیره شود.
- refresh token باید hash شده ذخیره شود.
- refresh token باید rotate شود.
- refresh token قبلی بعد از refresh موفق revoke شود.
- refresh token قبلی باید `RevokeReason = rotated` بگیرد.
- اگر refresh token قبلاً revoke/rotate شده دوباره استفاده شود، reuse detection فعال می‌شود.
- در reuse detection، همه refresh tokenهای فعال همان user revoke می‌شوند.
- در reuse detection، refresh tokenهای فعال باید `RevokeReason = reuse_detected` بگیرند.
- در reuse detection، response باید `401 Unauthorized` باشد.
- در reuse detection، audit event زیر ثبت می‌شود:

```text
Auth.RefreshReuseDetected
```

#### Responses

| Status | Meaning |
|---|---|
| `200 OK` | refresh موفق |
| `400 Bad Request` | body ناقص |
| `401 Unauthorized` | refresh token نامعتبر، expire، revoke شده یا reuse شده |

#### Audit

| Action | When |
|---|---|
| `Auth.RefreshSucceeded` | refresh موفق - planned |
| `Auth.RefreshFailed` | refresh ناموفق - planned |
| `Auth.RefreshReuseDetected` | استفاده دوباره از refresh token revoke/rotate شده - implemented |

---

### POST `/api/auth/revoke`

#### Request

```json
{
  "refresh_token": "<refresh-token>"
}
```

#### Response 200

```json
{
  "ok": true
}
```

#### Responses

| Status | Meaning |
|---|---|
| `200 OK` | revoke idempotent |
| `400 Bad Request` | body ناقص |
| `401 Unauthorized` | token نامعتبر یا دسترسی نامعتبر |

#### Audit

| Action | When |
|---|---|
| `Auth.TokenRevoked` | token revoke شد - planned |

---

## Updated Tenant Contract

### GET `/api/diagnostics/tenant-test/{companyCode}`

این endpoint با attribute زیر محافظت می‌شود:

```csharp
[RequireTenantMatch]
```

در صورت mismatch بین route و claim:

| Status | Meaning |
|---|---|
| `403 Forbidden` | tenant violation |

Audit:

```text
Security.TenantViolation
```

---

### GET `/api/companies/{companyCode}`

اطلاعات یک شرکت.

#### Status

Active

#### Authentication

JWT required

#### Authorization

`Companies.Read`

#### Tenant Check

Yes, via:

```csharp
[RequireTenantMatch]
```

#### Rules

- مقدار `companyCode` در route باید با claim شرکت کاربر برابر باشد.
- حالت پیش‌فرض strict است.
- اگر claim شرکت وجود نداشته باشد یا معتبر نباشد، پاسخ `403 Forbidden` برمی‌گردد.
- اگر route companyCode با claim شرکت برابر نباشد، پاسخ `403 Forbidden` برمی‌گردد.
- tenant violation باید audit شود.

#### Responses

| Status | Meaning |
|---|---|
| `200 OK` | شرکت برگشت |
| `401 Unauthorized` | JWT نامعتبر یا وجود ندارد |
| `403 Forbidden` | tenant violation یا policy violation |
| `404 Not Found` | شرکت پیدا نشد |

#### Audit

| Action | When |
|---|---|
| `Security.TenantViolation` | mismatch یا نبود claim معتبر tenant |

---

## Implemented Audit Actions

| Action Type | Implemented | Tested |
|---|---:|---:|
| `Security.AccessDenied` | Yes | Yes |
| `Security.LoginRateLimited` | Yes | Yes |
| `Auth.RefreshReuseDetected` | Yes | Yes |
| `Security.TenantViolation` | Yes | Yes |

---

## Reports Gateway Requirement

قبل از فعال شدن Reports باید prefix زیر به Gateway اضافه شود:

```json
"/api/reports"
```

Reports نباید داخل CompaniesController پیاده‌سازی شود.

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

## Updated Definition of Done برای هر endpoint

هر endpoint فقط وقتی کامل حساب می‌شود که موارد زیر را داشته باشد:

```text
1. Route مشخص
2. DTO مشخص
3. Policy مشخص
4. Tenant check، اگر لازم است
5. [RequireTenantMatch]، اگر tenant-scoped است
6. Audit، اگر operation حساس است
7. تست 401
8. تست 403
9. تست موفق
10. تست tenant violation، اگر لازم است
11. تست audit، اگر audit لازم است
12. ثبت در ApiContract.md
13. ثبت در SecurityMatrix.md
14. اگر prefix جدید است، ثبت در Gateway allowlist
15. هماهنگی با Runbook، اگر endpoint در smoke/deploy test استفاده می‌شود
```

---

## Change Log Update

| Date | Change |
|---|---|
| 2026-04-30 | تکمیل Security Foundation: rate limit, refresh reuse, tenant violation audit, audit action types |
