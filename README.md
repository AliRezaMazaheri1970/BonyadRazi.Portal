# BonyadRazi.Portal

پرتال سازمانی با معماری Gateway + API Services + WebApp، با تمرکز روی:
- امنیت استاندارد (JWT/Policy/Default Deny)
- Tenant Isolation
- Audit واقعی برای 401/403 و عملیات‌های مهم
- CI/PR Gate روی GitHub

---

## معماری کلی
- **Gateway**: تنها نقطه در معرض اینترنت (YARP + rate-limit + allowlist)
- **Auth/API Service**: احراز هویت JWT + policy authorization + tenant enforcement
- **WebApp**: کلاینت پرتال

---

## Quick Start (Local)

### پیش‌نیاز
- .NET SDK (ترجیحاً نسخه‌ای که پروژه با آن تنظیم شده)
- دسترسی به DB ها (در صورت نیاز) یا اجرای تست‌ها با InMemory در محیط Testing

### متغیرهای محیطی ضروری
حداقل:
- `JWT_SIGNING_KEY` (حداقل 32 کاراکتر)

در Production/Stage معمولاً:
- `AUTH_DB_CONNECTION_STRING`
- `LABORATORY_RASF_CONNECTION_STRING`

> نکته امنیتی: هیچ کلید/ConnectionString واقعی نباید داخل repo یا appsettings commit شود.

---

## امنیت (خلاصه اجرایی)

### JWT Validation
- ValidateIssuer / ValidateAudience / ValidateLifetime = true
- Issuer: `BonyadRazi.Auth`
- Audience: `BonyadRazi.Portal`
- ClockSkew: 30s
- MapInboundClaims = false
- RoleClaimType = `ClaimTypes.Role`
- NameClaimType = `sub`

### Default Deny
- `FallbackPolicy = RequireAuthenticatedUser()` (در API)
- در Gateway: مسیرهای Public مشخص و باقی مسیرها محافظت‌شده هستند (Allow prefixes + JWT check)

### Tenant Isolation
- Tenant فقط از claim `company_code`
- نمونه: `GET /api/companies/{companyCode:guid}`
  - claim نبود یا mismatch → 403
  - match → 200

### Audit 401/403
- Middleware برای 401/403 رکورد Audit با ActionType=`SecurityDenied` ثبت می‌کند.
- گزارش Admin-only:
  - `GET /api/audit/denied` با فیلترهای `fromUtc/toUtc/statusCode/companyCode/page/pageSize`

---

## Documentation
- `docs/SecurityMatrix.md` — مرجع اجرایی Endpoint ها، Policy ها، TenantScope و ActionType
- `docs/SecurityIncidentRunbook.md` — دستورالعمل واکنش به رخدادهای امنیتی

---

## CI / PR Gate

### GitHub Actions
Workflow امنیتی:
- `.github/workflows/security-auth-tests.yml`

این workflow حداقل این‌ها را بررسی می‌کند:
- build در Release
- اجرای **SecurityTests**
- اجرای **GatewayTests**

### Branch Protection / Ruleset
- Ruleset فعال است و Status check زیر برای merge به `master` اجباری است:
  - `security-auth-tests / test`

### PR Template
- قالب PR در مسیر زیر نگهداری می‌شود:
  - `.github/pull_request_template.md`

---

## Testing

### SecurityTests
- بدون توکن → 401
- cross-tenant → 403
- same-tenant → 200
- audit denied: بدون توکن 401، غیرادمین 403، ادمین 200

### GatewayTests
- `/gateway/health` → 200 (Public)
- auth endpoints (login/refresh/revoke) → 200 (Public)
- protected endpoint بدون JWT → 401
- protected endpoint با JWT → 200
- IP allowlist (no match) → 403

### نکته Testing DB
در CI/Testing از EFCore InMemory استفاده می‌شود تا تست‌ها به DB واقعی وابسته نباشند.

---

## مسیرهای کلیدی
- Gateway routes: `/api/auth/*`, `/api/users/*`, `/api/companies/*`, `/api/audit/*`
- WebApp routes: `/login`, `/dashboard`

---

## Contributing
قبل از افزودن هر Endpoint جدید:
1) ثبت در `docs/SecurityMatrix.md`
2) tenant enforcement (در صورت نیاز)
3) audit ActionType
4) تست‌های 401/403 و مسیر مجاز
5) سبز شدن CI قبل از PR merge