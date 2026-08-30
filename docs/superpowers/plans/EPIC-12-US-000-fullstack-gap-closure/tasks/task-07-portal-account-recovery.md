# Task 07 - Portal Account Recovery

**Status:** Ready  
**Closes gaps:** Forgot password link.

## Files

- Backend domain: new password reset token entity or Identity token provider integration
- Backend API: `AuthController.cs`
- Frontend API: `common/src/lib/auth/auth.api.ts`
- Frontend UI: admin and portal auth routes/components

## Implementation

- Add request reset endpoint with non-enumerating response.
- Add complete reset endpoint with token validation.
- Send email notification through existing notification gateway.
- Add request and complete forms in portal/admin apps.

## Code Example

```csharp
public sealed record RequestPasswordResetCommand(string Email)
    : IRequest<Response<Unit>>;
```

```ts
requestPasswordReset(email: string): Observable<unknown> {
  return this.http.post('/api/Auth/password-reset/request', { email });
}
```

## Acceptance

- [ ] Login screens link to password reset.
- [ ] Unknown and known email receive same response shape.
- [ ] Token expires and is single-use.
- [ ] New password follows policy.
- [ ] Audit records reset completion, not token value.

## Evidence

Pending.
