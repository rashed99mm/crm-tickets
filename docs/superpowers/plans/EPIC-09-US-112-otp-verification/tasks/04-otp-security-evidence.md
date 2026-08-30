# Task 04 — OTP Public Security and Evidence

**Criteria:** `OTP-7`, `OTP-9`

## Steps

1. Add rate limits by contact/IP for request and verify routes.
2. Return account-enumeration-safe messages for unknown contacts.
3. Assert response bodies and logs contain no plaintext code, contact secrets, provider credentials,
   or provider response body.
4. Run focused and full verification; paste actual output into the story record.

**Run:** `dotnet build backend/CustomerSupport.slnx --warnaserror` then
`dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~Otp"`  
**Expected:** Clean build, focused tests pass, and leakage assertions pass.

**Commit:** `test: verify otp security boundaries`
