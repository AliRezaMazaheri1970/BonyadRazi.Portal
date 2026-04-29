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