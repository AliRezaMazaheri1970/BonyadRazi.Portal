
---

## 2) docs/SecurityIncidentRunbook.md

```md
# Security Incident Runbook — BonyadRazi.Portal

این Runbook برای پاسخ عملیاتی به رخدادهای امنیتی نوشته شده است:
- Spike در 401/403 (به‌خصوص `SecurityDenied`)
- Spike در `LoginFailed`
- نشانه‌های Cross-Tenant attempt
- خطاهای ناخواسته 500 در مسیرهای امنیتی

---

## 0) تعاریف و سیگنال‌ها
### سیگنال‌های اصلی
- **SecurityDenied**: هر 401/403 که توسط Middleware ثبت می‌شود.
- **LoginFailed**: تلاش ناموفق ورود (به‌خصوص در حجم بالا).
- **TokenRefresh / TokenRevoke**: نوسان غیرعادی می‌تواند نشانه سوء‌استفاده باشد.

### شدت رخداد (Severity)
- **SEV1**: افزایش شدید `LoginFailed` + IP های متنوع / حمله گسترده، یا شواهد دسترسی غیرمجاز موفق
- **SEV2**: افزایش شدید `SecurityDenied` (401/403) یا Cross-Tenant attempt بدون موفقیت
- **SEV3**: خطاهای موردی/گزارش کاربر بدون الگوی حمله

---

## 1) چک فوری (۱۵ دقیقه اول)
1) تایم‌اسپن بررسی را مشخص کنید (مثلاً ۳۰ دقیقه اخیر UTC)
2) از Endpoint گزارش استفاده کنید:
   - `GET /api/audit/denied?fromUtc=...&toUtc=...&statusCode=401|403&companyCode=...`
3) نرخ‌ها را جدا کنید:
   - 401 (No token / Expired / Invalid)
   - 403 (Policy deny / Cross-tenant / Role deny)
4) Top offender ها:
   - `remoteIp`
   - `userAgent`
   - `path`
   - `reason`

---

## 2) تشخیص نوع رخداد

### A) Spike در 401
علت‌های رایج:
- کلاینت بدون توکن به endpoint های Protected
- AccessToken منقضی شده و Refresh درست کار نمی‌کند
- Gateway/YARP اشتباه route می‌دهد یا header Authorization را نمی‌فرستد
- Issuer/Audience mismatch بعد از تغییر config

اقدامات:
1) بررسی کنید آیا مسیرهای Public درست هستند (login/refresh/revoke)
2) نمونه یک درخواست 401 را با TraceId پیدا کنید و log سرویس API را match کنید
3) صحت ENV ها:
   - `JWT_SIGNING_KEY` (حداقل 32 کاراکتر)
   - `Jwt:Issuer = BonyadRazi.Auth`
   - `Jwt:Audience = BonyadRazi.Portal`
4) اگر پشت Gateway هستید:
   - بررسی کنید Authorization header حذف/تغییر نشده باشد
   - ForwardedHeaders برای ثبت IP واقعی فعال باشد

### B) Spike در 403
علت‌های رایج:
- Policy/Role misconfiguration
- tenant mismatch (Cross-Tenant attempt یا bug در claim)
- endpoint جدید بدون ثبت policy درست

اقدامات:
1) در گزارش denied فیلتر کنید `statusCode=403`
2) `reason` را بررسی کنید:
   - `CrossTenant` → تلاش دسترسی بین‌شرکتی (یا bug)
   - `PolicyDenied`/`RoleDenied` → تنظیمات Policy/Role
3) روی endpoint موردنظر تست کنید:
   - same-tenant → باید 200
   - cross-tenant → باید 403
4) اگر 403 برای same-tenant رخ می‌دهد:
   - بررسی claim `company_code`
   - بررسی RoleClaimType و NameClaimType
   - بررسی اینکه `FallbackPolicy` فعال است و endpoint اجازه‌نامه درست دارد

### C) Spike در LoginFailed
علت‌های رایج:
- brute force
- credential stuffing
- مشکل UX (کاربران واقعی رمز را اشتباه می‌زنند) ولی با IP ثابت

اقدامات:
1) Top IP و UserAgent را استخراج کنید
2) اگر IP های زیاد + الگوی ثابت userAgent:
   - SEV1/SEV2 در نظر بگیرید
3) Rate limit های gateway را بررسی کنید (login/refresh/revoke)
4) Lockout policy را بررسی کنید (اگر فعال است، نسبت false positive را بسنجید)

---

## 3) اقدامات مهار (Containment)
- در Gateway:
  - افزایش سخت‌گیری Rate Limit برای login (موقت)
  - مسدودسازی IP/ASN مشکوک (اگر سیاست سازمان اجازه می‌دهد)
- در API:
  - بررسی اینکه secrets در log/audit ثبت نشده باشند
  - در صورت مشکوک بودن حساب‌ها:
    - revoke session / refresh tokens (طبق مکانیزم جاری)

---

## 4) بررسی ریشه‌ای (RCA) — چک‌لیست
- آیا تغییر اخیر در:
  - Issuer/Audience
  - `JWT_SIGNING_KEY`
  - gateway routes
  - policy mapping
  - tenant enforcement
  باعث رخداد شده؟
- آیا endpoint جدید بدون ثبت در SecurityMatrix اضافه شده؟
- آیا CI سبز بوده و PR Gate رعایت شده؟

---

## 5) خروجی استاندارد حادثه (Incident Report Template)
- Incident ID:
- Severity:
- Timeline (UTC):
- Impact:
- Indicators (IPs, paths, traceIds):
- Containment actions:
- Root Cause:
- Fix applied (PR links):
- Preventive actions (tests/docs):