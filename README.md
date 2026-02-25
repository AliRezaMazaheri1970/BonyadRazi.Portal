# BonyadRazi.Portal

پرتال بنیاد رازی — معماری Microservice با Gateway (YARP) + API + لایه‌های Domain/Application/Infrastructure/Shared  
تمرکز اصلی: **امنیت (JWT/Policy/Tenant Isolation)**، **Audit در DB** و **CI Security Gate**

---

## ساختار پروژه

- `Api/` سرویس API (Auth/JWT + Controllers + Policies)
- `Gateway/` Gateway مبتنی بر YARP (allowlist, rate limit, health)
- `Domain/` مدل‌های دامنه
- `Application/` منطق کاربردی و Abstractions
- `Infrastructure/` Persistence, Audit, EF Core
- `Shared/` Contracts/Shared primitives
- `Tests/BonyadRazi.Portal.SecurityTests/` تست‌های امنیتی (CI Gate)

---

## پیش‌نیازها

- .NET SDK (پروژه روی `net10.0` تنظیم شده است)
- (در صورت اجرای واقعی DB) SQL Server برای دیتابیس Audit

---

## اجرای لوکال

### متغیرهای محیطی ضروری (JWT)

نمونه برای PowerShell:

```powershell
$env:JWT_SIGNING_KEY="YOUR_32+_CHAR_SECRET_KEY________________"
$env:Jwt__Issuer="BonyadRazi.Auth"
$env:Jwt__Audience="BonyadRazi.Portal"