# Security Incident Runbook

این سند راهنمای اجرایی واکنش به رخدادهای امنیتی در پروژه **BonyadRazi.Portal** است.

## 1) تعریف رخداد
رخداد امنیتی یعنی هر اتفاقی که یکی از موارد زیر را نشان دهد:
- تلاش غیرعادی برای ورود (LoginFailed) از یک IP یا روی یک حساب
- تلاش برای دسترسی cross-tenant (403 مکرر)
- افزایش ناگهانی `401/403` روی مسیرهای حساس
- نشانه‌های سوءاستفاده از Refresh/Revocation یا حملات brute force

## 2) منابع داده و لاگ
- Gateway logs
- Api logs
- Audit logs (ActionType + Metadata)

### فیلدهای لازم در Metadata
- ip
- userAgent
- path
- traceId
- isSuccess

> قانون: هرگز Password/AccessToken/RefreshToken داخل Metadata ذخیره نشود.

## 3) مراحل واکنش سریع (Triage)
1. **تأیید رخداد**: تعداد رویدادهای `LoginFailed`/`SecurityDenied` را در بازه زمانی بررسی کنید.
2. **شناسایی Scope**:
   - حساب/کاربر هدف
   - IP یا رنج IP
   - endpointهای تحت حمله
3. **مهار موقت (Containment)**:
   - مسدودسازی IP/Range در لایه شبکه یا allowlist
   - اگر لازم است RateLimit را سخت‌تر کنید (login/refresh)
   - در صورت احتمال compromise، کاربر را lock کنید یا رمز را reset کنید
4. **جمع‌آوری شواهد**:
   - export لاگ‌ها با timestamp UTC
   - traceIdهای مرتبط

---

## Incident Playbook: Spike in 401/403 (SecurityDenied)

### هدف
تشخیص سریع علت افزایش 401/403 و اقدام عملی بدون downtime.

### ابزار اصلی (Admin-only)
Endpoint گزارش denied:
- `GET /api/audit/denied`
- فیلترها: `fromUtc`, `toUtc`, `statusCode`, `companyCode`, `page`, `pageSize`

### گام‌های عملی (Step-by-step)

#### Step 1 — بازه زمانی را کوچک کن
مثلاً ۱۵ دقیقه اخیر:
- `fromUtc = nowUtc - 15m`
- `toUtc = nowUtc`

نمونه:
- `/api/audit/denied?page=1&pageSize=50&fromUtc=2026-02-25T12:00:00Z&toUtc=2026-02-25T12:15:00Z`

#### Step 2 — تفکیک 401 و 403
- فقط 401:
  - `/api/audit/denied?page=1&pageSize=50&statusCode=401&fromUtc=...&toUtc=...`
- فقط 403:
  - `/api/audit/denied?page=1&pageSize=50&statusCode=403&fromUtc=...&toUtc=...`

**تعبیر سریع:**
- 401 بالا → توکن نامعتبر/منقضی، misconfig issuer/audience، یا اسکن مسیرها
- 403 بالا → cross-tenant، policy/role mismatch، یا تلاش برای admin-only

#### Step 3 — مسیرهای پرتکرار را پیدا کن
در خروجی report، `path` و `method` را مرتب کن (ذهنی/دستی) و ببین کدام endpointها هدف هستند.
- اگر مسیرهای عجیب زیاد است → احتمال اسکن/Probe
- اگر مسیرهای tenant-scoped زیاد است (companies/users) → احتمال cross-tenant یا client misconfig

#### Step 4 — منبع را بررسی کن
- `remoteIp` پرتکرار؟
- `userAgent` ثابت/غیرعادی؟
- `traceId` برای نمونه‌برداری و ردیابی دقیق در لاگ‌های Api/Gateway

#### Step 5 — اقدام مهار (Containment)
- اگر یک IP/Range مشخص عامل deny هاست:
  - مسدودسازی در لایه شبکه / Firewall / IP allowlist
- اگر 401 ناشی از misconfig است:
  - بررسی `Jwt:Issuer`, `Jwt:Audience`, `JWT_SIGNING_KEY` در محیط Production
- اگر 403 ناشی از cross-tenant است:
  - بررسی claim `company_code` در JWT و جریان صدور توکن
  - بررسی اینکه route companyCode صرفاً با claim مقایسه می‌شود (Tenant Guard)

### Root Cause Checklist (چک‌لیست علت ریشه‌ای)
**401**
- issuer/audience mismatch
- signing key mismatch
- clock skew / exp / nbf
- استفاده از توکن قدیمی در client

**403**
- role/policy ناقص
- تلاش cross-tenant
- دسترسی admin-only توسط user عادی

---

## 4) اقدام اصلاحی (Eradication)
- بررسی ضعف‌های مسیرهای درگیر (policy, tenant guard, allowlist)
- بررسی سوءپیکربندی (JWT issuer/audience، clock skew، signing key)
- اطمینان از عدم leak شدن secretها (ENV/Secret Store)

## 5) بازیابی (Recovery)
- بازگرداندن دسترسی‌های ضروری پس از اطمینان
- مانیتورینگ شدیدتر برای 24-72 ساعت

## 6) درس‌آموخته‌ها (Postmortem)
- ثبت timeline رخداد
- علت ریشه‌ای (Root Cause)
- اقدام‌های پیشگیرانه (tests، rules، monitoring)
- آپدیت کردن Security Matrix در صورت تغییر API