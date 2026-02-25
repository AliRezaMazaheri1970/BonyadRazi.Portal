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
- [ ] هیچ endpoint غیرضروری public نشده (خصوصاً health/openapi).

### JWT / Claims / Policy
- [ ] endpointهای protected فقط با JWT معتبر کار می‌کنند.
- [ ] issuer/audience مطابق config پروژه است.
- [ ] Policy مناسب روی endpoint اعمال شده است.
- [ ] Role/Policy ادمین (Admin/SuperAdmin) برای مسیرهای admin-only درست enforce شده.

### Tenant Isolation (Cross-Tenant)
- [ ] برای مسیرهای tenant-scoped: `company_code` فقط از Claim خوانده می‌شود.
- [ ] company_code از route/query/body به عنوان مبنا استفاده نشده مگر کنترل‌شده.
- [ ] سناریوی cross-tenant تست شده و 403 می‌دهد.
- [ ] سناریوی same-tenant تست شده و 200 می‌دهد.

### Audit / Logging
- [ ] برای 401/403، `SecurityDenied` ثبت می‌شود.
- [ ] Metadata شامل `statusCode`, `path`, `method`, `ip`, `userAgent`, `traceId` است.
- [ ] هیچ Password/Token/Secret داخل Metadata ذخیره نشده است.

### Secrets Hygiene
- [ ] Secret جدید نیاز شد؟ فقط در ENV/SecretStore/GitHub Secrets اضافه شد (نه داخل repo).
- [ ] ConnectionString واقعی یا کلید امنیتی داخل appsettings commit نشده است.

---

## تست‌ها (قبل از merge باید سبز باشد)
- [ ] `dotnet test -c Release` سبز است.
- [ ] SecurityTests پوشش دارد:
  - [ ] unauthorized (بدون JWT) → 401
  - [ ] cross-tenant → 403
  - [ ] same-tenant → 200
  - [ ] admin-only: بدون توکن 401 / غیرادمین 403 / ادمین 200

---

## تغییرات در مستندات
- [ ] `docs/SecurityMatrix.md` به‌روزرسانی شد (اگر endpoint تغییر کرده)
- [ ] در صورت تغییر رفتار امنیتی، `docs/SecurityIncidentRunbook.md` هم sync شد

---

## توضیحات تکمیلی / ریسک‌ها
- ریسک‌های احتمالی:
- Plan Rollback: