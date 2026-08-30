# Task 05 — Verification route, authorization and contract tests

**Criteria:** `AC-439`, `AC-440`, `AC-443`, `AC-444`, `AC-445`, `AC-446`  
**Commit:** `feat(api): expose safe otp verification endpoint`

## Files

- Add `backend/src/CustomerSupport.ExternalApi/Controllers/VerificationController.cs` if the
  approved public-flow policy keeps verification on the external host.
- If authenticated staff verification is the only approved flow, add the action to
  `backend/src/CustomerSupport.InternalApi/Controllers/AuthController.cs` and document why no
  external route exists.
- Change shared rate-limit/configuration files under
  `backend/src/CustomerSupport.Api.Shared/` only for an approved policy.
- Add `backend/tests/CustomerSupport.Tests/Integration/OtpVerificationEndpointTests.cs`.

## Execution steps

1. Write HTTP tests for 200, safe invalid-code failure, malformed request, unknown id, unauthorized
   access and standard response envelope.
2. Add explicit `ProducesResponseType` declarations for every response status.
3. Ensure the route never logs code/contact secrets and never returns the stored hash.
4. Confirm the profile `PUT /api/Auth/me` is `[Authorize]` and remains separate from Admin-only
   `PUT /api/Users/{id}`.

## Live contract assertion

```csharp
var json = await response.Content.ReadAsStringAsync();
json.Should().NotContain("123456");
json.Should().NotContain("codeHash");
response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
```

## Run

```text
dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~OtpVerificationEndpointTests"
```
