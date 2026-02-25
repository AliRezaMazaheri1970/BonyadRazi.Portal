## خلاصه تغییرات
- 

## نوع تغییر
- [ ] Security / Auth
- [ ] Gateway / YARP
- [ ] API
- [ ] Infrastructure / Persistence
- [ ] Shared / Contracts
- [ ] CI / Tests
- [ ] Docs (SecurityMatrix / Runbook)

---

## چک‌لیست امنیتی (الزامی)

### Default Deny / Exposure
- [ ] Endpoint جدید اضافه شده؟ اگر بله در `docs/SecurityMatrix.md` ثبت شد.
- [ ] مسیرهای جدید در Gateway allowlist / routes تعریف شده‌اند (Default Deny حفظ شده).
- [ ] مسیرهای health/openapi/public ناخواسته عمومی نشده‌اند.

### JWT / Policy
- [ ] مسیرهای protected فقط با JWT معتبر پاسخ می‌دهند.
- [ ] Policy مناسب روی endpoint اعمال شده است.
- [ ] اگر endpoint admin-only است، دسترسی غیرادمین 403 می‌دهد.

### Tenant Isolation (اگر tenant-scoped است)
- [ ] `company_code` فقط از Claim خوانده می‌شود (نه از route/query/body به عنوان مبنا).
- [ ] cross-tenant → 403
- [ ] same-tenant → 200

### Audit / Logging
- [ ] برای 401/403، `SecurityDenied` ثبت می‌شود.
- [ ] metadata شامل `statusCode`, `path`, `method`, `ip`, `userAgent`, `traceId` است.
- [ ] هرگز Password/AccessToken/RefreshToken در metadata ذخیره نشده است.

### Secrets Hygiene
- [ ] هیچ Secret/ConnectionString واقعی داخل repo/appsettings commit نشده است.
- [ ] Secret جدید؟ فقط ENV/SecretStore/GitHub Secrets.

---

## تست‌ها (قبل از merge باید سبز باشد)
- [ ] `dotnet test -c Release` سبز است.
- [ ] SecurityTests پوشش دارد:
  - [ ] unauthorized (بدون JWT) → 401
  - [ ] cross-tenant → 403
  - [ ] same-tenant → 200
  - [ ] admin-only: بدون توکن 401 / غیرادمین 403 / ادمین 200

---

## Docs / مستندات
- [ ] اگر endpoint/policy تغییر کرده، `docs/SecurityMatrix.md` آپدیت شده است.
- [ ] اگر رفتار Incident/Logging تغییر کرده، `docs/SecurityIncidentRunbook.md` آپدیت شده است.

---

## ریسک‌ها / Rollback
- ریسک‌های احتمالی:
- Plan rollback: