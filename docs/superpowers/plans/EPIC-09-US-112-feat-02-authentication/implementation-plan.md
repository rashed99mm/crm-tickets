# FEAT-02 Authentication (backend) Implementation Plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

**Goal:** Sign-in issuing JWT access + refresh tokens, registration, refresh, logout, current-user, and change-password — the identity half of `FEAT-02` (`AC-1`..`AC-6`, `AC-54`..`AC-56`).

**Spec:** `docs/superpowers/specs/EPIC-09-US-112-auth-management-design.md` (auth slice).

**Architecture:** `AuthController` (InternalApi) → MediatR commands in `Application/Features/Auth`; `ITokenService` / `IRefreshTokenService` / `IIdentityUserService` implemented in `Infrastructure.Identity`; JWT validated by `AddPlatformAuthentication` (foundation Task 4).

## Global constraints

- Tokens carry `roles` claim (ASP.NET default role claim URI) so the frontend `roleGuard` works.
- Access-token expiry is returned as an ISO-8601 string (`AC-54`), never parsed client-side.
- Refresh token is a stored `RefreshToken` row, single-flight refresh on the client (see frontend plan).

## Task 1 — Sign-in command + handler (`AC-1`, `AC-54`)

**Files:**
- `backend/src/CustomerSupport.InternalApi/Controllers/AuthController.cs` (`login`)
- `backend/src/CustomerSupport.Application/Features/Auth/Commands/Login/{LoginCommand,LoginCommandHandler,LoginCommandValidator}.cs`
- `backend/src/CustomerSupport.Application/Features/Auth/Dtos/AuthResponse.cs`

**Interfaces:** `LoginCommand(string Email, string Password, string? IpAddress, string? UserAgent) : ICommand<Response<AuthResponse>>`.

**Step 1 — Real handler (excerpt)**

```csharp
// backend/src/CustomerSupport.Application/Features/Auth/Commands/Login/LoginCommandHandler.cs
public class LoginCommandHandler(
    IIdentityUserService identityUserService, ITokenService tokenService,
    IRefreshTokenService refreshTokenService, IMessageFactory messages, ILogger<LoginCommandHandler> logger)
    : ICommandHandler<LoginCommand, Response<AuthResponse>>
{
    public async Task<Response<AuthResponse>> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await identityUserService.FindByEmailAsync(request.Email, ct);
        if (user == null)
            return messages.Fail<AuthResponse>(ApplicationErrors.Auth.INVALID_CREDENTIALS, MessageType.Unauthorized);
        if (!user.IsActive)
            return messages.Fail<AuthResponse>(ApplicationErrors.Auth.ACCOUNT_DEACTIVATED, MessageType.Forbidden);
        if (!await identityUserService.CheckPasswordAsync(user, request.Password))
            return messages.Fail<AuthResponse>(ApplicationErrors.Auth.INVALID_CREDENTIALS, MessageType.Unauthorized);

        var roles = await identityUserService.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.Email!, roles, null);
        var refreshToken = await _refreshTokenService.CreateRefreshTokenAsync(user.Id, request.IpAddress, request.UserAgent, ct);
        user.RecordLogin();
        return messages.Success(new AuthResponse(
            user.Id, user.Email!, user.FirstName, user.LastName,
            accessToken, refreshToken.Token,
            _tokenService.GetTokenExpiration(accessToken), refreshToken.ExpiresAt, roles.ToList()),
            ApplicationErrors.Auth.LOGIN_SUCCESS);
    }
}
```

**Step 2 — Controller (real)**

```csharp
[HttpPost("login")]
[AllowAnonymous]
[EnableRateLimiting("login")]
public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    => this.ToActionResult(await _mediator.Send(
        new LoginCommand(request.Email, request.Password, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString()), ct));
```

- [ ] **Step 3: Run — integration login test**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AuthLoginEndpointTests"`
Expected: PASS — valid creds → 200 with `accessToken`/`refreshToken`; wrong password → 401.

- [ ] **Step 4: Commit:**

```bash
git add backend/src/CustomerSupport.InternalApi/Controllers/AuthController.cs \
        backend/src/CustomerSupport.Application/Features/Auth/
git commit -m "feat(auth): sign-in issuing JWT access + refresh (AC-1, AC-54)"
```

## Task 2 — Refresh, logout, current-user, change-password (`AC-55`, `AC-56`)

**Files:** `Features/Auth/Commands/{RefreshToken,Logout,ChangePassword}/*`, `Features/Auth/Queries/GetCurrentUser/*`, `AuthController`.

**Step 1 — Real endpoints**

```csharp
[HttpPost("refresh")] [AllowAnonymous]
public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
    => this.ToActionResult(await _mediator.Send(
        new RefreshTokenCommand(request.AccessToken, request.RefreshToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString()), ct));

[HttpPost("logout")] [Authorize]
public async Task<IActionResult> Logout([FromBody] LogoutRequest? request, CancellationToken ct)
    => this.ToActionResult(await _mediator.Send(new LogoutCommand(request?.RefreshToken), ct));

[HttpGet("me")] [Authorize]
public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    => this.ToActionResult(await _mediator.Send(new GetCurrentUserQuery(), ct));

[HttpPost("change-password")] [Authorize]
public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    => this.ToActionResult(await _mediator.Send(
        new ChangePasswordCommand(User.GetRequiredUserId(), request.CurrentPassword, request.NewPassword), ct));
```

`ChangePasswordCommandHandler` revokes other sessions after updating — matching `AUTH` self-service semantics.

- [ ] **Step 2: Run:** `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AuthRefresh"`
Expected: PASS — refresh with valid token yields new pair; logout revokes the refresh row.

- [ ] **Step 3: Commit:** `git commit -m "feat(auth): refresh/logout/me/change-password (AC-55, AC-56)"`

## Task 3 — Token validation (`AC-` security)

`AddPlatformAuthentication` (foundation Task 4) validates issuer/audience/lifetime/signing key from `Jwt:Key`. The inherited `ITokenService.GenerateAccessToken` writes `roles` via the ASP.NET role claim URI, which is exactly what the frontend `SessionStore.decodeClaims` reads.

- [ ] **Step 4: Run — protected endpoint refuses without token**

Run: `curl -i http://localhost:5074/api/Users` without `Authorization`
Expected: 401, envelope `code` set.

- [ ] **Step 5: Commit:** `git commit -m "feat(auth): JWT validation wired (foundation Task 4)"`

## Self-review

Coverage: `AC-1`, `AC-54` → Task 1; `AC-55`,`AC-56` → Task 2; validation → Task 3.

**Discrepancy found:** the old plan's tasks were "token issuer / identity sign-in / token validation / sign-in endpoint / protected endpoint / credential hygiene" as if hand-built. The shipped code is the inherited platform's `Auth/*` command set plus `AddPlatformAuthentication`; there is no hand-written token issuer. Naming in the rewrite now matches the real `LoginCommandHandler`/`AuthController` shape.
