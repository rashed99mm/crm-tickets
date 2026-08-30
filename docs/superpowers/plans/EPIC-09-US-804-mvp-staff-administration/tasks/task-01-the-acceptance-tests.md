# Task 1 — Write the acceptance tests, and find out what is actually true

| Field | Value |
|---|---|
| Plan | [`implementation-plan.md`](../implementation-plan.md) |
| Story | `MVP-02` Administer staff accounts and roles |
| Criteria | 1, 2, 3, 4, 5 of [`epic-1-staff-access.md`](../../../../requirements/mvp/epic-1-staff-access.md) |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- **new** `backend/tests/CustomerSupport.Tests/Integration/StaffAdministrationTests.cs`

## Test evidence — final run

```
Passed  StaffAdministrationTests.MVP02_Admin_CreatesStaffWithAnAgentRole            [109 ms]
Passed  StaffAdministrationTests.MVP02_Admin_CreatesStaffWithASupervisorRole        [ 81 ms]
Passed  StaffAdministrationTests.MVP02_NonAdmin_IsRefusedTheStaffSurface            [216 ms]
Passed  StaffAdministrationTests.MVP02_DeactivatedStaff_CannotSignIn                [190 ms]
Passed  StaffAdministrationTests.MVP02_DeactivatedStaff_KeepTheirHistory            [191 ms]
Passed  StaffAdministrationTests.MVP02_DeactivatedAgent_IsNotOfferedAsAnAssignee    [218 ms]
Passed  StaffAdministrationTests.MVP02_DeactivatedAgent_CannotBeHandedWorkDirectly  [500 ms]
Passed  StaffAdministrationTests.MVP02_NoResponseCarriesAPasswordOrHash             [193 ms]

Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8
```

Whole suite, after the fix in [task 2](task-02-close-the-assignment-hole.md):

```
Passed!  - Failed:     0, Passed:   270, Skipped:     0, Total:   270, Duration: 1 m 26 s
```

262 before this task, 270 after — the eight tests above, no existing test disturbed.

## Which test proves which criterion

| Criterion | Test | Verdict |
|---|---|---|
| 1 — an administrator creates staff as `Agent` or `Supervisor` | `MVP02_Admin_CreatesStaffWithAnAgentRole`, `..._WithASupervisorRole` | held as inherited |
| 2 — a deactivated employee cannot sign in | `MVP02_DeactivatedStaff_CannotSignIn` | held as inherited |
| 2 — their history stays intact | `MVP02_DeactivatedStaff_KeepTheirHistory` | held as inherited |
| 3 — non-administrators are refused the endpoints | `MVP02_NonAdmin_IsRefusedTheStaffSurface` | held as inherited |
| 4 — a deactivated agent is not offered as an assignee | `MVP02_DeactivatedAgent_IsNotOfferedAsAnAssignee` (the picker) and `MVP02_DeactivatedAgent_CannotBeHandedWorkDirectly` (the mutation) | **the picker held; the mutation did not — see task 2** |
| 5 — no response carries a password or its hash | `MVP02_NoResponseCarriesAPasswordOrHash` | held as inherited |

## Criterion 2 was the one the plan told us to watch, and it holds

The plan's warning was well founded in general and wrong about this platform. `ApplicationUser.IsActive`
*is* a custom field that ASP.NET Identity's sign-in path knows nothing about — but
`LoginCommandHandler` checks it explicitly, **before** the password check, and returns
`ACCOUNT_DEACTIVATED` as a 403. `RefreshTokenCommandHandler` checks it too, which is the leg that
would otherwise have made the login check pointless: refusing the password while honouring an
already-issued refresh token would leave a departed employee renewing their session indefinitely.
The test drives both legs.

So the `ACCOUNT_DEACTIVATED` code in `ApplicationErrors.Auth` was not decoration. Verified by test
rather than trusted, which was the point.

## Tests written so they could fail

- **The first sign-in in `..._CannotSignIn` is load-bearing.** The staff member signs in
  successfully *before* being deactivated. Without that leg, a test that only ever observed a
  refusal could not distinguish "deactivation worked" from "these credentials never worked".
- **`..._IsNotOfferedAsAnAssignee` reads the picker twice**, before and after the deactivation, and
  asserts on its own subject's id. Asserting only on the "after" state would pass against a
  `GetUsersInRoleAsync` that returned an empty list for any reason.
- **`..._IsRefusedTheStaffSurface` uses a `Supervisor`, not an anonymous caller.** The near miss is
  the case worth testing: per ADR-0012 the most senior support role in the product is still not an
  administrator. It drives five routes across four verbs, then re-reads the subject as the
  administrator to confirm nothing changed — a 403 over a completed write is the failure a
  status-code-only assertion would miss.
- **Criterion 5 is swept over raw JSON strings**, never a typed model. Deserialising into `UserDto`
  and finding no password would only prove that `UserDto` has no password property; it says nothing
  about what the serialiser put on the wire. The needles are `password`, `passwordHash`,
  `securityStamp` and **the value actually submitted** — the likeliest leak here is not a hash but
  an echo.

## One deliberate asymmetry in the criterion 5 sweep

The success bodies (create, list, detail) are swept case-insensitively for the *word* `password`.
The rejection body is swept only for the submitted *value*.

That is not laziness. Identity's own complaints legitimately contain the word — "Passwords must be
at least 8 characters" — so sweeping the word on the failure path would produce a red test over
correct behaviour, and the next reader would fix it by weakening the assertion. The leak that
matters on a refusal is a handler echoing back the credential it was given, and that is what is
asserted.

## Test design notes

- **Fixtures go through `/api/Users`, not `UserManager`.** The criteria are about what an
  administrator can do over the wire; a fixture that created users directly through Identity would
  prove nothing about the endpoint under test.
- **Sign-ins are counted per test.** `/api/Auth/login` is rate limited to five attempts per five
  minutes per caller. `CrmApiFactory` is constructed per test instance, so each test gets a fresh
  host and a fresh limiter; every test here stays at three sign-ins or fewer, and
  `..._IsNotOfferedAsAnAssignee` reads the picker as the administrator (the `Admin` role satisfies
  the `Supervisor` policy) specifically to avoid a second one.
- **Real LocalDB**, like the rest of the CRM suite: role membership, `IsActive` and the history
  foreign keys are all things the real provider enforces and the in-memory one does not.

## Observations recorded, not fixed

None of these is an `MVP-02` criterion. They are written down because they were seen while looking,
and a finding nobody records is a finding nobody acts on.

1. **An access token issued before deactivation stays valid until it expires** — `Jwt:ExpirationMinutes`
   defaults to 60 (`TokenService`). Sign-in and refresh are both closed, so the window cannot be
   extended, but a departed employee keeps up to an hour of API access. Closing it means checking
   `IsActive` per request or revoking on deactivation; both are design changes beyond this pass.
2. **Deactivation does not revoke the user's stored refresh tokens.** The rows are left behind and
   made inert by the `IsActive` check in `RefreshTokenCommandHandler`. Revoking them would be
   defence in depth rather than a fix.
3. **`ACCOUNT_DEACTIVATED` tells a caller the account exists.** Correct and useful for a staff
   member who has been deactivated; it is also account enumeration, and it sits next to `MVP-01`
   criterion 2, which requires sign-in failures not to reveal whether an account exists. Worth
   deciding deliberately for `MVP-01` rather than inheriting.
4. **A password below policy answers `INTERNAL_ERROR`, not a field-keyed validation error.**
   `CreateUserCommandHandler` funnels every Identity failure into `INTERNAL_ERROR` with the
   descriptions under `details.technicalErrors`. It is a 400 and it leaks no credential, so
   criterion 5 is unaffected — but there is no `CreateUserCommand` validator, and a staff-creation
   form cannot key that message to the password field (`US-104`).
5. **`TreatWarningsAsErrors` is not set** in `backend/Directory.Build.props`, though the project
   instructions describe "shipped" as including a clean build under warnings-as-errors. The build
   emits nine pre-existing warnings (`ContentSpecifications`, `ExternalApi*`, the RabbitMQ host
   configurator) — none from this task's changes.
