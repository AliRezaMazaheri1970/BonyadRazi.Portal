## Summary
- What does this PR change?

## Security checklist (required)
- [ ] No secrets committed (keys, tokens, connection strings)
- [ ] Default Deny preserved (protected endpoints require auth)
- [ ] Tenant Isolation enforced where needed (`company_code` claim)
- [ ] Audit events added/updated where relevant (401/403 are covered)
- [ ] Logs/Audit do not include secrets/PII

## Tests
- [ ] `dotnet test -c Release` passed locally
- [ ] CI status checks passed

## Notes / Risk
- Impact scope:
- Rollback plan: