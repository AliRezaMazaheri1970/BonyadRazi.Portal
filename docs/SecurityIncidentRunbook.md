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
