# Task 06 — Migration, security audit and backend evidence

**Criteria:** `AC-432`, `AC-433`, `AC-434`, `AC-435`, `AC-436`, `AC-437`, `AC-439`…`AC-446`  
**Commit:** `test(security): evidence profile and otp verification boundaries`

## Files

- Add the reviewed EF migration under
  `backend/src/CustomerSupport.Infrastructure/Migrations/`.
- Add/update integration tests in `backend/tests/CustomerSupport.Tests/Integration/`.
- Update `docs/superpowers/plans/EPIC-09-US-112-profile-update-and-otp-verification/README.md` with
  actual output only.
- Update the relevant story/spec traceability only after tests and build have run.

## Execution steps

1. Run the focused unit and integration tests against the configured relational test database.
2. Generate the migration only after reviewing the model and inspect both `Up` and `Down` for
   destructive operations.
3. Build with warnings as errors.
4. Run the full backend suite.
5. Search source, logs and serialized responses for plaintext OTP, token, password and code-hash
   leakage.

## Commands

```text
dotnet ef migrations add AddOtpVerification --project src/CustomerSupport.Infrastructure --startup-project src/CustomerSupport.InternalApi
dotnet build CustomerSupport.slnx --warnaserror
dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~Profile|FullyQualifiedName~Otp"
dotnet test CustomerSupport.slnx
```

Do not mark this task complete until the actual output is pasted into the plan record.
