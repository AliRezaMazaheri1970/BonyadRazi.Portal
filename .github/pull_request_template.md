## خلاصه تغییرات
- 

## چک‌لیست امنیتی (الزامی)
- [ ] API جدید اضافه شده؟ اگر بله: در `docs/SecurityMatrix.md` ثبت شد.
- [ ] مسیرهای جدید در Gateway allowlist / routes تعریف شده‌اند (Default Deny حفظ شده).
- [ ] Policy مناسب روی endpoint اعمال شده است.
- [ ] Tenant isolation برای مسیرهای tenant-scoped رعایت شده (`company_code` فقط از claim).
- [ ] Audit ActionType/Metadata برای عملیات مهم اضافه شده (بدون ذخیره Token/Password).

## تست‌ها
- [ ] تست بدون JWT (Unauthorized)
- [ ] تست cross-tenant (Forbid)
- [ ] تست same-tenant (Ok)

## نکته‌ها
- اگر secret جدیدی نیاز است: فقط ENV/Secret Store (نه appsettings).
