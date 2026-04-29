# BonyadRazi Portal - API Contract

## هدف سند

این سند قرارداد API پروژه **BonyadRazi Portal** را ثبت می‌کند.

هدف این است که توسعه API، Gateway، تست‌های امنیتی، Runbook و WebApp آینده بر اساس یک قرارداد مشخص جلو بروند.

هر endpoint جدید باید قبل یا همزمان با پیاده‌سازی در این سند ثبت شود.

---

## وضعیت سند

| مورد | مقدار |
|---|---|
| Project | BonyadRazi.Portal |
| Scope | API only |
| Last Updated | 2026-04-29 |
| Current Phase | API Stabilization |
| Gateway | YARP |
| API Production Address | `http://127.0.0.1:6001` |
| Gateway Production Address | `http://192.168.0.103:6000` |
| Public/Internal DNS | `customers.razi-foundation.com` |

---

## معماری درخواست

مسیر درخواست در وضعیت فعلی:

```text
Client
  ↓
HAProxy
  ↓
Gateway / YARP
  ↓
API
```

مسیر production فعلی:

```text
Client
  ↓
HAProxy 192.168.0.42:80
  ↓
Gateway / YARP 192.168.0.103:6000
  ↓
API 127.0.0.1:6001
```

WebApp فعلاً خارج از scope این سند است و بعد از تثبیت API اضافه خواهد شد.

معماری آینده بعد از WebApp:

```text
Client
  ↓
HAProxy
  ↓
Gateway / YARP
  ├─ /api/*  → API     127.0.0.1:6001
  └─ /*      → WebApp  127.0.0.1:6002
```

---

## اصول کلی API

- تمام endpointهای `/api/*` فقط از مسیر Gateway در دسترس هستند.
- API در Production فقط روی `127.0.0.1:6001` اجرا می‌شود.
- Gateway روی `192.168.0.103:6000` اجرا می‌شود.
- HAProxy تنها ورودی شبکه داخلی است.
- endpointهای محافظت‌شده باید JWT معتبر داشته باشند.
- Gateway قبل از proxy کردن، API allowlist و JWT را بررسی می‌کند.
- API علاوه بر Gateway، خودش هم JWT و Policy را enforce می‌کند.
- tenant-scoped endpointها باید `company_code` را از JWT بخوانند.
- برای کاربران tenant-scoped، `companyCode` ورودی کاربر منبع اعتماد نیست.
- عملیات حساس باید Audit شوند.
- اطلاعات حساس مثل password، token، cookie و Authorization header نباید در Audit ذخیره شوند.
- پاسخ‌ها باید WebApp-friendly و قابل پیش‌بینی باشند.
- هر endpoint جدید باید تست 401، تست 403 و تست موفق داشته باشد.

---

## Naming و Route Convention

### Base API Prefix

تمام APIهای business زیر این prefix هستند:

```http
/api
```

### Route Style

قواعد routeها:

```text
- lowercase
- plural resource names
- بدون فعل در مسیر، مگر برای actionهای مشخص مثل reset-password
- استفاده از kebab-case برای action segmentها
```

نمونه درست:

```http
GET  /api/users
POST /api/users/{userId}/reset-password
GET  /api/reports/company/invoices
```

نمونه نامناسب:

```http
GET /api/GetUsers
POST /api/user/resetPassword
GET /api/companyInvoiceReport
```

---

## Authentication

### روش احراز هویت

API از JWT Bearer استفاده می‌کند.

Header استاندارد:

```http
Authorization: Bearer <access-token>
```

### JWT Claims

حداقل claimهای مورد انتظار:

| Claim | Required | Description |
|---|---:|---|
| `sub` | Yes | شناسه کاربر |
| `role` | Yes | نقش یا نقش‌های کاربر |
| `company_code` | For tenant users | شناسه شرکت کاربر |
| `jti` | Recommended | شناسه یکتای access token |
| `name` | Optional | نام کاربر یا نام نمایشی |

### رفتار استاندارد

| وضعیت | Response |
|---|---|
| JWT وجود ندارد | `401 Unauthorized` |
| JWT نامعتبر است | `401 Unauthorized` |
| JWT expire شده است | `401 Unauthorized` |
| JWT معتبر است ولی role کافی نیست | `403 Forbidden` |
| JWT معتبر است ولی tenant اشتباه است | `403 Forbidden` |

---

## Common Headers

### Request Headers

| Header | Required | Description |
|---|---:|---|
| `Authorization` | For protected endpoints | JWT Bearer token |
| `Content-Type` | For request body | معمولاً `application/json` |
| `Accept` | Optional | معمولاً `application/json` |

### Forwarded Headers

Forwarded headers باید توسط HAProxy تنظیم شوند، نه client:

| Header | Source | Description |
|---|---|---|
| `X-Forwarded-For` | HAProxy | IP واقعی client |
| `X-Forwarded-Proto` | HAProxy | `http` یا `https` |
| `X-Forwarded-Host` | HAProxy | host اصلی request |
| `X-Real-IP` | HAProxy | IP واقعی client |

API و Gateway نباید به forwarded headerهای client-provided اعتماد کنند، مگر از proxyهای شناخته‌شده.

---

## Common Response Rules

### Date/Time

تمام تاریخ‌ها باید UTC باشند.

فرمت پیشنهادی:

```text
ISO 8601 UTC
```

نمونه:

```json
{
  "createdAtUtc": "2026-04-29T12:00:00Z"
}
```

### Paging

برای endpointهای list، خروجی باید صفحه‌بندی داشته باشد.

Query استاندارد:

```http
?page=1&pageSize=20
```

Response استاندارد:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0
}
```

قواعد پیشنهادی:

| Field | Rule |
|---|---|
| `page` | حداقل `1` |
| `pageSize` | پیش‌فرض `20` |
| `pageSize` max | حداکثر `100` |

### Sorting

در صورت نیاز:

```http
?sortBy=createdAtUtc&sortDirection=desc
```

مقادیر مجاز `sortDirection`:

```text
asc
desc
```

### Search

در صورت نیاز:

```http
?search=term
```

---

## Error Response Standard

برای خطاهای validation و framework، استفاده از `ProblemDetails` پیشنهاد می‌شود.

نمونه:

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "Validation failed",
  "status": 400,
  "traceId": "00-...",
  "correlationId": "...",
  "errors": {
    "username": [
      "Username is required."
    ]
  }
}
```

برای خطاهای business، قالب پیشنهادی:

```json
{
  "success": false,
  "message": "امکان غیرفعال کردن آخرین مدیر سیستم وجود ندارد.",
  "errorCode": "LastAdminCannotBeDisabled",
  "correlationId": "..."
}
```

---

## HTTP Status Standard

| Status | Meaning |
|---|---|
| `200 OK` | عملیات موفق |
| `201 Created` | ایجاد موفق |
| `204 No Content` | عملیات موفق بدون خروجی |
| `400 Bad Request` | ورودی نامعتبر یا request ناقص |
| `401 Unauthorized` | JWT وجود ندارد، نامعتبر است یا expire شده |
| `403 Forbidden` | دسترسی کافی نیست یا tenant violation |
| `404 Not Found` | مسیر یا داده پیدا نشد |
| `409 Conflict` | تداخل business مثل username تکراری |
| `423 Locked` | حساب کاربری lock شده |
| `500 Internal Server Error` | خطای غیرمنتظره |

---

# API Endpoints

## Health

### GET `/health`

وضعیت پایه API را برمی‌گرداند.

این endpoint برای smoke test است و نباید اطلاعات حساس، وضعیت دقیق دیتابیس، نام سرور، stack trace یا configuration برگرداند.

#### Status

Active

#### Authentication

Anonymous

#### Authorization

None

#### Response 200

```json
{
  "status": "ok",
  "where": "api",
  "utc": "2026-04-29T12:00:00Z"
}
```

#### Tests

| Test | Expected |
|---|---|
| `GET /health` | `200 OK` |

---

## Diagnostics

Diagnostics برای smoke test و تست لایه‌های امنیتی استفاده می‌شود.

این endpointها نباید جایگزین APIهای business شوند.

---

### GET `/api/diagnostics/auth-test`

تست می‌کند JWT معتبر توسط API پذیرفته می‌شود.

#### Status

Active

#### Authentication

Required

#### Authorization

Authenticated user

#### Tenant Check

No

#### Audit

No

#### Responses

| Status | Meaning |
|---|---|
| `200 OK` | JWT معتبر است |
| `401 Unauthorized` | JWT وجود ندارد یا نامعتبر است |

#### Response 200

```json
{
  "status": "ok",
  "where": "api",
  "check": "auth",
  "user": "username",
  "utc": "2026-04-29T12:00:00Z"
}
```

#### Tests

| Test | Expected |
|---|---|
| Without JWT | `401 Unauthorized` |
| With valid JWT | `200 OK` |
| Gateway without JWT | `401 Unauthorized` |
| Gateway with valid JWT | `200 OK` |

---

### GET `/api/diagnostics/admin-test`

تست می‌کند policy مربوط به مدیر سیستم درست کار می‌کند.

#### Status

Active

#### Authentication

Required

#### Authorization

`SystemAdmin`

#### Tenant Check

No

#### Audit

No

#### Responses

| Status | Meaning |
|---|---|
| `200 OK` | کاربر policy لازم را دارد |
| `401 Unauthorized` | JWT وجود ندارد یا نامعتبر است |
| `403 Forbidden` | کاربر احراز هویت شده ولی مجوز لازم را ندارد |

#### Response 200

```json
{
  "status": "ok",
  "where": "api",
  "check": "admin",
  "user": "username",
  "utc": "2026-04-29T12:00:00Z"
}
```

---

### GET `/api/diagnostics/tenant-test/{companyCode}`

تست می‌کند tenant isolation درست کار می‌کند.

#### Status

Active

#### Authentication

Required

#### Authorization

Authenticated user

#### Tenant Check

Yes

#### Audit

No

#### Route Parameters

| Name | Type | Required | Description |
|---|---|---:|---|
| `companyCode` | `guid` | Yes | شناسه شرکت مورد تست |

#### Rules

- اگر کاربر `Admin` یا `SuperAdmin` باشد، می‌تواند از tenant check عبور کند.
- اگر کاربر معمولی باشد، مقدار `companyCode` در route باید با claim شرکت کاربر برابر باشد.
- اگر claim شرکت وجود نداشته باشد یا معتبر نباشد، پاسخ `403 Forbidden` برمی‌گردد.

#### Responses

| Status | Meaning |
|---|---|
| `200 OK` | tenant check موفق |
| `401 Unauthorized` | JWT وجود ندارد یا نامعتبر است |
| `403 Forbidden` | tenant violation یا claim نامعتبر |

#### Response 200

```json
{
  "status": "ok",
  "where": "api",
  "check": "tenant",
  "routeCompanyCode": "11111111-1111-1111-1111-111111111111",
  "claimCompanyCode": "11111111-1111-1111-1111-111111111111",
  "utc": "2026-04-29T12:00:00Z"
}
```

#### Tests

| Test | Expected |
|---|---|
| Without JWT | `401 Unauthorized` |
| Same tenant | `200 OK` |
| Cross tenant with normal user | `403 Forbidden` |

---

## Auth

Auth ستون اصلی API است و قبل از WebApp باید کامل و پایدار شود.

### Endpoint Summary

| Method | Endpoint | Status | Auth | Audit |
|---|---|---|---|---:|
| `POST` | `/api/auth/login` | Existing | Anonymous | Yes |
| `POST` | `/api/auth/refresh` | Existing | RefreshToken | Yes |
| `POST` | `/api/auth/revoke` | Existing | Depends | Yes |
| `POST` | `/api/auth/logout` | Planned | JWT | Yes |
| `POST` | `/api/auth/logout-all` | Planned | JWT | Yes |
| `GET` | `/api/auth/me` | Planned | JWT | Optional |
| `POST` | `/api/account/change-password` | Planned | JWT | Yes |

---

### POST `/api/auth/login`

ورود کاربر و صدور access token و refresh token.

#### Status

Existing

#### Authentication

Anonymous

#### Authorization

PublicAuth

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
  "accessToken": "<jwt>",
  "refreshToken": "<refresh-token>",
  "expiresAtUtc": "2026-04-29T12:30:00Z",
  "user": {
    "id": "11111111-1111-1111-1111-111111111111",
    "username": "customer1",
    "displayName": "Customer User",
    "roles": [
      "Customer"
    ],
    "companyCode": "22222222-2222-2222-2222-222222222222"
  }
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

#### Audit

| Action | When |
|---|---|
| `Auth.LoginSucceeded` | ورود موفق |
| `Auth.LoginFailed` | ورود ناموفق |

#### Security Notes

- password نباید در log یا audit ذخیره شود.
- login باید rate limit داشته باشد.
- خطای login نباید مشخص کند username وجود دارد یا نه.

---

### POST `/api/auth/refresh`

صدور access token جدید با refresh token معتبر.

#### Status

Existing

#### Authentication

Refresh token

#### Request

```json
{
  "refreshToken": "<refresh-token>"
}
```

#### Response 200

```json
{
  "accessToken": "<jwt>",
  "refreshToken": "<new-refresh-token>",
  "expiresAtUtc": "2026-04-29T13:00:00Z"
}
```

#### Rules

- refresh token باید hash شده در دیتابیس ذخیره شود.
- refresh token باید rotate شود.
- refresh token قبلی بعد از refresh موفق revoke شود.
- replay باید قابل تشخیص باشد.
- IP و UserAgent باید برای refresh ثبت شوند.

#### Responses

| Status | Meaning |
|---|---|
| `200 OK` | refresh موفق |
| `400 Bad Request` | body ناقص |
| `401 Unauthorized` | refresh token نامعتبر، expire یا revoke شده |

#### Audit

| Action | When |
|---|---|
| `Auth.RefreshSucceeded` | refresh موفق |
| `Auth.RefreshFailed` | refresh ناموفق |

---

### POST `/api/auth/revoke`

revoke کردن refresh token.

#### Status

Existing

#### Authentication

Depends on implementation

#### Request

```json
{
  "refreshToken": "<refresh-token>"
}
```

#### Response 204

No body.

#### Responses

| Status | Meaning |
|---|---|
| `204 No Content` | revoke موفق |
| `400 Bad Request` | body ناقص |
| `401 Unauthorized` | token نامعتبر یا دسترسی نامعتبر |

#### Audit

| Action | When |
|---|---|
| `Auth.TokenRevoked` | token revoke شد |

---

### POST `/api/auth/logout`

خروج از session فعلی.

#### Status

Planned

#### Authentication

JWT required

#### Request

```json
{
  "refreshToken": "<refresh-token>"
}
```

#### Response 204

No body.

#### Rules

- refresh token فعلی revoke شود.
- access token تا زمان expire معتبر می‌ماند، مگر blacklist با jti اضافه شود.

#### Audit

`Auth.Logout`

---

### POST `/api/auth/logout-all`

خروج از همه دستگاه‌ها.

#### Status

Planned

#### Authentication

JWT required

#### Request

No body.

#### Response 204

No body.

#### Rules

- تمام refresh tokenهای فعال کاربر revoke شوند.
- برای تغییر رمز یا رخداد امنیتی مهم استفاده می‌شود.

#### Audit

`Auth.LogoutAll`

---

### GET `/api/auth/me`

اطلاعات کاربر جاری برای WebApp.

#### Status

Planned

#### Authentication

JWT required

#### Response 200

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "username": "customer1",
  "displayName": "Customer User",
  "roles": [
    "Customer"
  ],
  "companyCode": "22222222-2222-2222-2222-222222222222",
  "permissions": [
    "companies.read",
    "reports.invoices.read"
  ]
}
```

#### Responses

| Status | Meaning |
|---|---|
| `200 OK` | اطلاعات کاربر برگشت |
| `401 Unauthorized` | JWT وجود ندارد یا نامعتبر است |

---

### POST `/api/account/change-password`

تغییر رمز کاربر جاری.

#### Status

Planned

#### Authentication

JWT required

#### Request

```json
{
  "currentPassword": "OldP@ssw0rd!",
  "newPassword": "NewP@ssw0rd!"
}
```

#### Response 204

No body.

#### Rules

- رمز فعلی باید درست باشد.
- رمز جدید باید password policy را پاس کند.
- رمز جدید نباید با رمزهای اخیر یکسان باشد، اگر password history فعال شد.
- بعد از تغییر رمز، logout-all اختیاری یا اجباری باید طبق policy مشخص شود.

#### Responses

| Status | Meaning |
|---|---|
| `204 No Content` | تغییر رمز موفق |
| `400 Bad Request` | ورودی نامعتبر |
| `401 Unauthorized` | JWT نامعتبر |
| `403 Forbidden` | رمز فعلی اشتباه است یا policy اجازه نمی‌دهد |

#### Audit

`Auth.PasswordChanged`

---

## Users

Users API برای مدیریت کاربران داخلی، کاربران شرکت‌ها و عملیات امنیتی روی حساب‌ها استفاده می‌شود.

### Endpoint Summary

| Method | Endpoint | Status | Auth | Tenant Check | Audit |
|---|---|---|---|---:|---:|
| `GET` | `/api/users` | Planned | Admin / CompanyAdmin | Yes | Yes |
| `GET` | `/api/users/{userId}` | Planned | Admin / CompanyAdmin | Yes | Yes |
| `POST` | `/api/users` | Planned | Admin / CompanyAdmin | Yes | Yes |
| `PUT` | `/api/users/{userId}` | Planned | Admin / CompanyAdmin | Yes | Yes |
| `PATCH` | `/api/users/{userId}/status` | Planned | Admin / CompanyAdmin | Yes | Yes |
| `POST` | `/api/users/{userId}/reset-password` | Planned | Admin / CompanyAdmin | Yes | Yes |
| `POST` | `/api/users/{userId}/unlock` | Planned | Admin / CompanyAdmin | Yes | Yes |

---

### GET `/api/users`

لیست کاربران.

#### Status

Planned

#### Authentication

JWT required

#### Authorization

Admin / CompanyAdmin

#### Query Parameters

| Name | Type | Required | Description |
|---|---|---:|---|
| `search` | string | No | جستجو در username/displayName |
| `companyCode` | guid | No | فقط برای Admin |
| `role` | string | No | فیلتر نقش |
| `isActive` | bool | No | فیلتر فعال/غیرفعال |
| `page` | int | No | پیش‌فرض `1` |
| `pageSize` | int | No | پیش‌فرض `20` |

#### Rules

- `SuperAdmin` و `Admin` می‌توانند کاربران شرکت‌های مختلف را ببینند.
- `CompanyAdmin` فقط کاربران شرکت خودش را می‌بیند.
- `Customer` عادی به این endpoint دسترسی ندارد.
- password/hash هرگز نباید برگردد.

#### Response 200

```json
{
  "items": [
    {
      "id": "11111111-1111-1111-1111-111111111111",
      "username": "customer1",
      "displayName": "Customer User",
      "companyCode": "22222222-2222-2222-2222-222222222222",
      "roles": [
        "Customer"
      ],
      "isActive": true,
      "createdAtUtc": "2026-04-29T12:00:00Z",
      "lastLoginAtUtc": "2026-04-29T12:10:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

#### Responses

| Status | Meaning |
|---|---|
| `200 OK` | لیست برگشت |
| `401 Unauthorized` | JWT نامعتبر |
| `403 Forbidden` | role کافی نیست |

#### Audit

`Users.Viewed` یا optional. برای کاهش حجم لاگ، audit لیست می‌تواند فقط برای admin actions فعال شود.

---

### GET `/api/users/{userId}`

جزئیات یک کاربر.

#### Status

Planned

#### Authentication

JWT required

#### Authorization

Admin / CompanyAdmin

#### Route Parameters

| Name | Type | Required |
|---|---|---:|
| `userId` | guid | Yes |

#### Response 200

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "username": "customer1",
  "displayName": "Customer User",
  "companyCode": "22222222-2222-2222-2222-222222222222",
  "roles": [
    "Customer"
  ],
  "isActive": true,
  "createdAtUtc": "2026-04-29T12:00:00Z",
  "updatedAtUtc": "2026-04-29T12:00:00Z",
  "lastLoginAtUtc": "2026-04-29T12:10:00Z"
}
```

#### Responses

| Status | Meaning |
|---|---|
| `200 OK` | کاربر برگشت |
| `401 Unauthorized` | JWT نامعتبر |
| `403 Forbidden` | role یا tenant نامعتبر |
| `404 Not Found` | کاربر پیدا نشد |

---

### POST `/api/users`

ایجاد کاربر جدید.

#### Status

Planned

#### Authentication

JWT required

#### Authorization

Admin / CompanyAdmin

#### Request

```json
{
  "username": "customer1",
  "displayName": "Customer User",
  "password": "P@ssw0rd!",
  "companyCode": "22222222-2222-2222-2222-222222222222",
  "roles": [
    "Customer"
  ],
  "isActive": true
}
```

#### Rules

- username باید unique باشد.
- password باید password policy را پاس کند.
- کاربر باید حداقل یک role داشته باشد.
- `CompanyAdmin` فقط برای شرکت خودش می‌تواند کاربر بسازد.
- `CompanyAdmin` نباید بتواند roleهای سیستمی مثل Admin/SuperAdmin بدهد.

#### Response 201

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "username": "customer1",
  "displayName": "Customer User",
  "companyCode": "22222222-2222-2222-2222-222222222222",
  "roles": [
    "Customer"
  ],
  "isActive": true,
  "createdAtUtc": "2026-04-29T12:00:00Z"
}
```

#### Responses

| Status | Meaning |
|---|---|
| `201 Created` | کاربر ایجاد شد |
| `400 Bad Request` | ورودی نامعتبر |
| `401 Unauthorized` | JWT نامعتبر |
| `403 Forbidden` | role یا tenant نامعتبر |
| `409 Conflict` | username تکراری |

#### Audit

`Users.Created`

---

### PUT `/api/users/{userId}`

ویرایش اطلاعات کاربر.

#### Status

Planned

#### Authentication

JWT required

#### Authorization

Admin / CompanyAdmin

#### Request

```json
{
  "displayName": "Customer User",
  "companyCode": "22222222-2222-2222-2222-222222222222",
  "roles": [
    "Customer"
  ]
}
```

#### Rules

- تغییر role باید کنترل شود.
- `CompanyAdmin` نباید بتواند کاربر را به شرکت دیگر منتقل کند.
- `CompanyAdmin` نباید roleهای سیستمی بدهد.
- تغییرات role باید Audit شود.

#### Response 200

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "username": "customer1",
  "displayName": "Customer User",
  "companyCode": "22222222-2222-2222-2222-222222222222",
  "roles": [
    "Customer"
  ],
  "isActive": true,
  "updatedAtUtc": "2026-04-29T12:00:00Z"
}
```

#### Audit

`Users.Updated`

اگر role تغییر کرد:

`Users.RoleChanged`

---

### PATCH `/api/users/{userId}/status`

فعال یا غیرفعال کردن کاربر.

#### Status

Planned

#### Authentication

JWT required

#### Authorization

Admin / CompanyAdmin

#### Request

```json
{
  "isActive": false,
  "reason": "User left company"
}
```

#### Rules

- آخرین `SuperAdmin` نباید غیرفعال شود.
- کاربر نباید بتواند خودش را غیرفعال کند، مگر policy مشخص شود.
- دلیل تغییر وضعیت بهتر است ثبت شود.

#### Response 204

No body.

#### Responses

| Status | Meaning |
|---|---|
| `204 No Content` | وضعیت تغییر کرد |
| `400 Bad Request` | ورودی نامعتبر |
| `403 Forbidden` | دسترسی کافی نیست |
| `404 Not Found` | کاربر پیدا نشد |
| `409 Conflict` | rule تجاری اجازه نمی‌دهد |

#### Audit

`Users.StatusChanged`

---

### POST `/api/users/{userId}/reset-password`

reset کردن رمز کاربر توسط مدیر.

#### Status

Planned

#### Authentication

JWT required

#### Authorization

Admin / CompanyAdmin

#### Request

```json
{
  "newPassword": "NewP@ssw0rd!",
  "forceChangeOnNextLogin": true
}
```

#### Rules

- password باید policy را پاس کند.
- `CompanyAdmin` فقط کاربران شرکت خودش را reset کند.
- reset password حتماً Audit شود.
- password جدید در response برنگردد.

#### Response 204

No body.

#### Audit

`Users.PasswordReset`

---

### POST `/api/users/{userId}/unlock`

باز کردن lock حساب کاربر.

#### Status

Planned

#### Authentication

JWT required

#### Authorization

Admin / CompanyAdmin

#### Request

```json
{
  "reason": "Verified by support"
}
```

#### Response 204

No body.

#### Audit

`Users.Unlocked`

---

## Companies

Companies API اطلاعات شرکت‌ها و تنظیمات مربوط به Portal را مدیریت می‌کند.

### مدل مفهومی

دو مفهوم جدا داریم:

```text
CompanyDirectory:
- اطلاعات مرجع شرکت از دیتابیس LaboratoryRasf یا منبع خارجی

CompanySettings:
- تنظیمات مربوط به Portal در دیتابیس Portal
```

---

### Endpoint Summary

| Method | Endpoint | Status | Auth | Tenant Check | Audit |
|---|---|---|---|---:|---:|
| `GET` | `/api/companies/me` | Planned | JWT | Yes | Optional |
| `GET` | `/api/companies/{companyCode}` | Existing / Planned | JWT | Yes | Yes |
| `GET` | `/api/companies/directory` | Planned | Admin | No | Yes |
| `PUT` | `/api/companies/{companyCode}/settings` | Planned | Admin | Yes | Yes |
| `GET` | `/api/companies/{companyCode}/users` | Planned | Admin / CompanyAdmin | Yes | Yes |

---

### GET `/api/companies/me`

اطلاعات شرکت کاربر جاری.

#### Status

Planned

#### Authentication

JWT required

#### Authorization

Authenticated

#### Tenant Check

Yes

#### Rules

- `companyCode` از claim کاربر خوانده می‌شود.
- کاربر tenant-scoped نباید companyCode را از query مشخص کند.

#### Response 200

```json
{
  "companyCode": "22222222-2222-2222-2222-222222222222",
  "companyName": "Example Company",
  "nationalId": "1234567890",
  "economicCode": "123456",
  "isPortalEnabled": true
}
```

---

### GET `/api/companies/{companyCode}`

اطلاعات یک شرکت.

#### Status

Existing / Planned

#### Authentication

JWT required

#### Authorization

Authenticated / Admin

#### Tenant Check

Yes

#### Rules

- `Customer` فقط شرکت خودش را می‌بیند.
- `CompanyAdmin` فقط شرکت خودش را می‌بیند.
- `Admin` و `SuperAdmin` می‌توانند شرکت‌های مختلف را ببینند.

#### Responses

| Status | Meaning |
|---|---|
| `200 OK` | شرکت برگشت |
| `401 Unauthorized` | JWT نامعتبر |
| `403 Forbidden` | tenant violation |
| `404 Not Found` | شرکت پیدا نشد |

#### Audit

`Companies.Viewed`

---

### GET `/api/companies/directory`

جستجوی directory شرکت‌ها.

#### Status

Planned

#### Authentication

JWT required

#### Authorization

Admin

#### Query Parameters

| Name | Type | Required | Description |
|---|---|---:|---|
| `search` | string | No | جستجوی نام، کد، شناسه ملی |
| `page` | int | No | شماره صفحه |
| `pageSize` | int | No | اندازه صفحه |

#### Response 200

```json
{
  "items": [
    {
      "companyCode": "22222222-2222-2222-2222-222222222222",
      "companyName": "Example Company",
      "nationalId": "1234567890",
      "economicCode": "123456"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

#### Audit

`Companies.DirectorySearched`

---

### PUT `/api/companies/{companyCode}/settings`

ویرایش تنظیمات Portal برای شرکت.

#### Status

Planned

#### Authentication

JWT required

#### Authorization

Admin

#### Request

```json
{
  "isPortalEnabled": true,
  "maxUsers": 10,
  "notes": "Enabled for pilot"
}
```

#### Response 200

```json
{
  "companyCode": "22222222-2222-2222-2222-222222222222",
  "isPortalEnabled": true,
  "maxUsers": 10,
  "notes": "Enabled for pilot",
  "updatedAtUtc": "2026-04-29T12:00:00Z"
}
```

#### Audit

`Companies.SettingsUpdated`

---

### GET `/api/companies/{companyCode}/users`

لیست کاربران یک شرکت.

#### Status

Planned

#### Authentication

JWT required

#### Authorization

Admin / CompanyAdmin

#### Tenant Check

Yes

#### Query Parameters

| Name | Type | Required |
|---|---|---:|
| `page` | int | No |
| `pageSize` | int | No |
| `search` | string | No |

#### Response 200

```json
{
  "items": [
    {
      "id": "11111111-1111-1111-1111-111111111111",
      "username": "customer1",
      "displayName": "Customer User",
      "roles": [
        "Customer"
      ],
      "isActive": true
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

---

## Reports

Reports API برای گزارش‌های شرکت، فاکتورها، قراردادها و workflow استفاده می‌شود.

Reports نباید داخل CompaniesController پیاده‌سازی شود.

### Gateway Requirement

قبل از فعال شدن Reports باید prefix زیر به Gateway اضافه شود:

```json
"/api/reports"
```

---

### Endpoint Summary

| Method | Endpoint | Status | Auth | Tenant Check | Audit |
|---|---|---|---|---:|---:|
| `GET` | `/api/reports/company/invoices` | Planned | Customer / Admin | Yes | Yes |
| `GET` | `/api/reports/company/invoices/{masterBillCode}` | Planned | Customer / Admin | Yes | Yes |
| `GET` | `/api/reports/company/invoices/{masterBillCode}/pdf` | Planned | Customer / Admin | Yes | Yes |
| `GET` | `/api/reports/company/contracts` | Planned | Customer / Admin | Yes | Yes |
| `GET` | `/api/reports/company/contracts/{contractCode}` | Planned | Customer / Admin | Yes | Yes |
| `GET` | `/api/reports/company/contracts/{contractCode}/document` | Planned | Customer / Admin | Yes | Yes |
| `GET` | `/api/reports/company/workflow` | Planned | Customer / Admin | Yes | Yes |
| `GET` | `/api/reports/company/workflow/{workflowId}` | Planned | Customer / Admin | Yes | Yes |

---

### GET `/api/reports/company/invoices`

لیست فاکتورهای شرکت.

#### Status

Planned

#### Authentication

JWT required

#### Authorization

Customer / CompanyAdmin / Admin

#### Tenant Check

Yes

#### Query Parameters

| Name | Type | Required | Description |
|---|---|---:|---|
| `fromDate` | date | No | تاریخ شروع |
| `toDate` | date | No | تاریخ پایان |
| `status` | string | No | وضعیت فاکتور |
| `companyCode` | guid | Admin only | فقط برای Admin |
| `page` | int | No | صفحه |
| `pageSize` | int | No | اندازه صفحه |

#### Rules

- برای Customer و CompanyAdmin، companyCode باید از JWT خوانده شود.
- `companyCode` query فقط برای Admin معتبر است.
- cross-tenant باید `403 Forbidden` بدهد.

#### Response 200

```json
{
  "items": [
    {
      "masterBillCode": 123456,
      "billNo": "INV-1405-0001",
      "billDate": "2026-04-29T00:00:00Z",
      "amount": 15000000,
      "status": "Issued"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

#### Audit

`Reports.InvoicesViewed`

---

### GET `/api/reports/company/invoices/{masterBillCode}`

جزئیات فاکتور.

#### Status

Planned

#### Authentication

JWT required

#### Tenant Check

Yes

#### Route Parameters

| Name | Type | Required |
|---|---|---:|
| `masterBillCode` | long/int | Yes |

#### Response 200

```json
{
  "masterBillCode": 123456,
  "billNo": "INV-1405-0001",
  "billDate": "2026-04-29T00:00:00Z",
  "amount": 15000000,
  "status": "Issued",
  "items": [
    {
      "description": "Laboratory service",
      "quantity": 1,
      "amount": 15000000
    }
  ]
}
```

#### Audit

`Reports.InvoiceViewed`

---

### GET `/api/reports/company/invoices/{masterBillCode}/pdf`

دانلود PDF فاکتور.

#### Status

Planned

#### Authentication

JWT required

#### Tenant Check

Yes

#### Response 200 Headers

```http
Content-Type: application/pdf
Content-Disposition: attachment; filename="invoice-123456.pdf"
Cache-Control: no-store
X-Content-Type-Options: nosniff
```

#### Rules

- قبل از تولید یا ارسال PDF، مالکیت tenant باید بررسی شود.
- فایل نباید در مسیر عمومی ذخیره شود.
- filename باید sanitize شود.
- دانلود باید Audit شود.

#### Audit

`Reports.InvoicePdfDownloaded`

---

### GET `/api/reports/company/contracts`

لیست قراردادهای شرکت.

#### Status

Planned

#### Authentication

JWT required

#### Tenant Check

Yes

#### Query Parameters

| Name | Type | Required |
|---|---|---:|
| `status` | string | No |
| `companyCode` | guid | Admin only |
| `page` | int | No |
| `pageSize` | int | No |

#### Response 200

```json
{
  "items": [
    {
      "contractCode": 1001,
      "contractNo": "C-1405-001",
      "title": "Service Contract",
      "status": "Active",
      "startDate": "2026-01-01T00:00:00Z",
      "endDate": "2026-12-31T00:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

#### Audit

`Reports.ContractsViewed`

---

### GET `/api/reports/company/contracts/{contractCode}`

جزئیات قرارداد.

#### Status

Planned

#### Authentication

JWT required

#### Tenant Check

Yes

#### Response 200

```json
{
  "contractCode": 1001,
  "contractNo": "C-1405-001",
  "title": "Service Contract",
  "status": "Active",
  "startDate": "2026-01-01T00:00:00Z",
  "endDate": "2026-12-31T00:00:00Z"
}
```

---

### GET `/api/reports/company/contracts/{contractCode}/document`

دانلود سند قرارداد.

#### Status

Planned

#### Authentication

JWT required

#### Tenant Check

Yes

#### Response Headers

```http
Content-Type: application/pdf
Content-Disposition: attachment; filename="contract-1001.pdf"
Cache-Control: no-store
X-Content-Type-Options: nosniff
```

#### Audit

`Reports.ContractDocumentDownloaded`

---

### GET `/api/reports/company/workflow`

لیست workflowهای شرکت.

#### Status

Planned

#### Authentication

JWT required

#### Tenant Check

Yes

#### Query Parameters

| Name | Type | Required |
|---|---|---:|
| `fromDate` | date | No |
| `toDate` | date | No |
| `companyCode` | guid | Admin only |
| `page` | int | No |
| `pageSize` | int | No |

#### Response 200

```json
{
  "items": [
    {
      "workflowId": 98765,
      "title": "Sample reception",
      "status": "InProgress",
      "createdAtUtc": "2026-04-29T12:00:00Z",
      "updatedAtUtc": "2026-04-29T12:10:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

#### Audit

`Reports.WorkflowViewed`

---

### GET `/api/reports/company/workflow/{workflowId}`

جزئیات workflow.

#### Status

Planned

#### Authentication

JWT required

#### Tenant Check

Yes

#### Response 200

```json
{
  "workflowId": 98765,
  "title": "Sample reception",
  "status": "InProgress",
  "steps": [
    {
      "name": "Registered",
      "status": "Done",
      "atUtc": "2026-04-29T12:00:00Z"
    }
  ]
}
```

---

## Audit

Audit API برای مشاهده رخدادهای امنیتی و عملیاتی استفاده می‌شود.

### Endpoint Summary

| Method | Endpoint | Status | Auth | Authorization |
|---|---|---|---|---|
| `GET` | `/api/audit/actions` | Planned | JWT | Admin / Auditor |
| `GET` | `/api/audit/logs` | Planned | JWT | Admin / Auditor |
| `GET` | `/api/audit/denied` | Existing | JWT | Admin / Auditor |
| `GET` | `/api/audit/users/{userId}` | Planned | JWT | Admin / Auditor |
| `GET` | `/api/audit/companies/{companyCode}` | Planned | JWT | Admin / Auditor / CompanyAdmin |

---

### GET `/api/audit/actions`

لیست action typeهای قابل فیلتر.

#### Status

Planned

#### Authentication

JWT required

#### Authorization

Admin / Auditor

#### Response 200

```json
{
  "items": [
    "Auth.LoginSucceeded",
    "Auth.LoginFailed",
    "Security.AccessDenied",
    "Reports.InvoicePdfDownloaded"
  ]
}
```

---

### GET `/api/audit/logs`

جستجوی audit logها.

#### Status

Planned

#### Authentication

JWT required

#### Authorization

Admin / Auditor

#### Query Parameters

| Name | Type | Required |
|---|---|---:|
| `fromUtc` | datetime | No |
| `toUtc` | datetime | No |
| `actionType` | string | No |
| `statusCode` | int | No |
| `userId` | guid | No |
| `companyCode` | guid | No |
| `remoteIp` | string | No |
| `page` | int | No |
| `pageSize` | int | No |

#### Response 200

```json
{
  "items": [
    {
      "id": 1,
      "utc": "2026-04-29T12:00:00Z",
      "actorUserId": "11111111-1111-1111-1111-111111111111",
      "actorUsername": "admin",
      "actionType": "Security.AccessDenied",
      "statusCode": 403,
      "method": "GET",
      "path": "/api/companies/22222222-2222-2222-2222-222222222222",
      "remoteIp": "192.168.93.3",
      "userAgent": "curl/8.0",
      "traceId": "...",
      "correlationId": "...",
      "reason": "Forbidden",
      "metadata": {
        "queryString": "[REDACTED]"
      }
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

#### Rules

- metadata باید redacted باشد.
- pageSize باید محدود شود.
- Customer عادی دسترسی ندارد.
- CompanyAdmin فقط در صورت نیاز می‌تواند audit شرکت خودش را ببیند.

---

### GET `/api/audit/denied`

مشاهده security denied events.

#### Status

Existing

#### Authentication

JWT required

#### Authorization

Admin / Auditor

#### Query Parameters

| Name | Type | Required |
|---|---|---:|
| `fromUtc` | datetime | No |
| `toUtc` | datetime | No |
| `remoteIp` | string | No |
| `page` | int | No |
| `pageSize` | int | No |

#### Response 200

همان قالب audit logs.

---

## Downloads

Downloadها فعلاً زیر Reports تعریف می‌شوند.

در آینده اگر API مستقل Documents لازم شد، prefix زیر اضافه می‌شود:

```http
/api/documents
```

### Download Security Rules

- JWT required.
- tenant ownership باید قبل از ارسال فایل بررسی شود.
- فایل نباید از مسیر عمومی سرو شود.
- filename باید sanitize شود.
- `Cache-Control: no-store` الزامی است.
- `X-Content-Type-Options: nosniff` الزامی است.
- هر download باید Audit شود.

---

## Gateway Contract

### Prefixهای فعلی

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

### Prefixهای آینده

```json
[
  "/api/reports"
]
```

### Routing فعلی

| Path | Destination |
|---|---|
| `/api/auth/*` | API |
| `/api/users/*` | API |
| `/api/companies/*` | API |
| `/api/audit/*` | API |
| `/api/diagnostics/*` | API |
| `/health` | API |
| `/gateway/health` | Gateway |

### Routing آینده

| Path | Destination |
|---|---|
| `/api/reports/*` | API |
| `/*` | WebApp |

---

## DTO Naming Convention

قواعد نام‌گذاری DTO:

```text
<Request>
- LoginRequest
- CreateUserRequest
- UpdateUserRequest
- ResetUserPasswordRequest

<Response>
- LoginResponse
- UserDetailsResponse
- CompanyDetailsResponse

<List Item>
- UserListItemDto
- CompanyListItemDto
- InvoiceListItemDto

<Paged>
- PagedResult<T>
```

---

## Validation Rules

### Required Fields

هر request باید validation روشن داشته باشد.

نمونه:

```json
{
  "username": "required",
  "password": "required"
}
```

### String Length

حداقل و حداکثر طول باید برای فیلدهای مهم مشخص شود:

| Field | Suggested Rule |
|---|---|
| `username` | 3 تا 100 کاراکتر |
| `password` | طبق password policy |
| `displayName` | 1 تا 200 کاراکتر |
| `notes` | حداکثر 1000 کاراکتر |

### GUID

اگر route parameter از نوع `guid` است، route constraint استفاده شود:

```http
/api/companies/{companyCode:guid}
```

---

## Security Redaction Contract

مقادیر زیر نباید در response، log یا audit خام ذخیره شوند:

```text
password
currentPassword
newPassword
token
accessToken
refreshToken
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

اگر query string شامل اطلاعات حساس باشد:

```json
{
  "queryString": "[REDACTED]"
}
```

---

## Test Contract

### برای هر endpoint محافظت‌شده

| Test | Expected |
|---|---|
| Without JWT | `401` |
| Invalid JWT | `401` |
| Expired JWT | `401` |
| Valid JWT but wrong role | `403` |
| Valid JWT but wrong tenant | `403` |
| Valid JWT and valid access | success status |

### برای هر endpoint tenant-scoped

| Test | Expected |
|---|---|
| Same tenant | success |
| Cross tenant normal user | `403` |
| Admin access | success |

### برای هر operation حساس

| Test | Expected |
|---|---|
| Operation success | audit record created |
| Sensitive data input | audit redacted |

---

## Definition of Done برای هر endpoint

هر endpoint فقط وقتی کامل حساب می‌شود که موارد زیر را داشته باشد:

```text
1. Route مشخص
2. DTO مشخص
3. Policy مشخص
4. Tenant check، اگر لازم است
5. Audit، اگر operation حساس است
6. تست 401
7. تست 403
8. تست موفق
9. تست tenant violation، اگر لازم است
10. ثبت در ApiContract.md
11. ثبت در SecurityMatrix.md
12. اگر prefix جدید است، ثبت در Gateway allowlist
13. هماهنگی با Runbook، اگر endpoint در smoke/deploy test استفاده می‌شود
```

---

## Change Log

| Date | Change |
|---|---|
| 2026-04-29 | تکمیل Health و Diagnostics |
| 2026-04-29 | اضافه شدن نقشه endpointهای Auth, Users, Companies, Reports, Audit |
