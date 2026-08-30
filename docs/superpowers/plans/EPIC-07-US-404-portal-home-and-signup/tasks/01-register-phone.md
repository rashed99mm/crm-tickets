# Task 01 — Add `PhoneNumber` to the register contract and persist it

**Story:** `US-401` (registration, backend contract) · **Criteria:** `ASG-8`
**Status:** done; verified by the end-of-work test run (single end-of-work verification run)

## Files

- Modify `backend/src/CustomerSupport.Application/Features/Auth/Dtos/AuthRequests.cs` —
  `RegisterRequest` gains `string? PhoneNumber`.
- Modify `backend/src/CustomerSupport.Application/Features/Auth/Commands/Register/RegisterCommand.cs` —
  gains `string? PhoneNumber`.
- Modify `backend/src/CustomerSupport.InternalApi/Controllers/AuthController.cs` — pass
  `request.PhoneNumber`.
- Modify `backend/src/CustomerSupport.Application/Features/Auth/Validators/RegisterCommandValidator.cs` —
  optional phone, `MaximumLength(20)` when present.
- Modify `backend/src/CustomerSupport.Application/Features/Auth/Commands/Register/RegisterCommandHandler.cs` —
  set `user.PhoneNumber` after `ApplicationUser.Create(...)`, before `CreateAsync`.
- Add/extend a backend integration test proving `ASG-8`.

## Implementation sequence

1. Failing integration test: register with `phoneNumber` persists it; absent/blank stays `null`;
   over-length phone → 400.
2. Thread the field through DTO → command → controller → validator → handler.
3. Normalize in the handler: trim; `null`/whitespace → `null` (never `""`, spec A4).

## Tests and evidence (to be pasted after the end-of-work run)

```text
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~Auth"
```

Test names: `ASG8_Register_PersistsPhoneNumber`, `ASG8_Register_BlankPhone_StaysNull`,
`ASG8_Register_OverLengthPhone_Returns400`.

## Notes / deviations

Recorded here as they occur.
