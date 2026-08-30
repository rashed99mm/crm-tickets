# Task 18 — API-key auth on ExternalApi (US-144)

## Spec reasoning
- Brief §11 (APIs / External systems): machine-to-machine read access to public surface.
- US-144: AC-144.1 (valid key → 200), AC-144.2 (invalid/missing → 401 envelope), AC-144.3 (scoped to ExternalApi, not InternalApi).
- BR-144.1: keys from configuration (user-secrets), never DB.
- BR-144.2: constant-time comparison via `CryptographicOperations.FixedTimeEquals`.
- BR-144.4: additive to JWT — existing anonymous + JWT flows unchanged.
- Plan step 1 already done: US-144 story filed at `docs/requirements/user-stories/US-144-external-api-key-auth.md`.

## Execution plan

### Step 1 — Add configuration key (done: story filed)

US-144 filed. No code yet.

### Step 2 — Add `ApiKeyAuthenticationHandler` middleware

**File to create:** `backend/src/CustomerSupport.ExternalApi/Middleware/ApiKeyAuthenticationHandler.cs`

Real code reference — look at the existing JWT bearer handler pattern in
`CustomerSupport.Api.Shared/Extensions/AuthenticationExtensions.cs` lines 10-40 for the scheme/handler
convention. The `ApiKeyAuthenticationHandler` must:
- Read header `X-Api-Key` via `context.Request.Headers["X-Api-Key"]`
- Compare with configured key using `CryptographicOperations.FixedTimeEquals` (byte comparison, not string equality)
- If valid, set `ClaimsPrincipal` with a fixed machine-client identity (e.g. `System:ApiKey`)
- If missing/invalid, do NOT call `next()` — set `context.Result = new AuthenticateResult.Fail(...)` so the 401 pipeline fires

Config key: `ExternalApi__ApiKey` (bound from user-secrets / environment).

### Step 3 — Register the scheme in DI

**File to modify:** `backend/src/CustomerSupport.ExternalApi/Program.cs`

After `AddPlatformAuthentication` (line 16), add:
```csharp
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", null);
builder.Services.AddAuthorization();
```
Change `app.UsePlatformPipeline()` to run after authentication is wired.

### Step 4 — Apply to knowledge-base and public endpoints

**File to modify:** `backend/src/CustomerSupport.ExternalApi/Controllers/KnowledgeBaseController.cs`

Add `[Authorize(AuthenticationSchemes = "ApiKey")]` to the controller class OR to specific endpoints that should accept the API key. Anonymous public endpoints (article list, article by slug) can accept both `ApiKey` and `Anonymous` schemes.

Actually: the ExternalApi already has anonymous endpoints here. The API-key scheme should be added as an **additional** scheme that can authenticate alongside anonymous. Use `[Authorize(AuthenticationSchemes = "ApiKey, Bearer")]` on the specific read endpoints that external systems need.

Simpler: add API-key as a global Default scheme that supplements (not replaces) the anonymous policy. The goal is: requests WITH a valid X-Api-Key are authenticated as the machine client; requests WITHOUT are processed anonymously if the endpoint allows it.

### Step 5 — Wire 401 envelope

The existing `ExceptionMiddleware` (first in pipeline, per CLAUDE.md notes) already converts auth failures to the standard envelope. Verify by checking `CustomerSupport.Api.Shared/Middleware/ExceptionMiddleware.cs` handles `UnauthorizedAccessException` and returns the envelope with the right `MessageType.Unauthorized`.

### Step 6 — Integration test

**Test file to create:** `backend/tests/CustomerSupport.Tests/Features/ExternalApi/ApiKeyAuthTests.cs`

- TC-01: GET /api/knowledge-base/articles with valid X-Api-Key → 200
- TC-02: same request without key → depends on endpoint anonymity
- TC-03: same request with invalid key → 401 envelope
- TC-04: same key against InternalApi → 401/403

Run via `dotnet test` — integration tests use WebApplicationFactory against the ExternalApi project.

## Gate
- [x] `dotnet build CustomerSupport.slnx` → 0 errors (2026-08-28)
- [ ] Integration tests named above green
- [ ] Existing anonymous portal flows (no key) unaffected
- [ ] TC-03 (invalid key → 401 envelope) verified against running host
