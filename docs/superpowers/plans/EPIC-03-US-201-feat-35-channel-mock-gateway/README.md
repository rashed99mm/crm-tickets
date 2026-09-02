# FEAT-35 plan 1 (outbound + mock/real toggle) — record

**Plan:** [`implementation-plan.md`](implementation-plan.md)
**Spec:** [`EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md`](../../specs/EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md)
— see the **Amendment — 2026-09-02** section for `CC-30`–`CC-51`.

## Status: Tasks 1–13 implemented and verified; not committed (explicit instruction).

## Criteria delivered

| Task | Criteria | Status |
|---|---|---|
| Task 1 | `CC-51` (closes `CC-10`, `CC-13`) | **done, verified, uncommitted** |
| Task 2 | `CC-48` | **done, verified, uncommitted** |
| Task 3 | `CC-30`–`CC-33` | **done, verified, uncommitted** |
| Task 4 | `CC-49` | **done, verified, uncommitted** |
| Task 5 | `CC-35`, `CC-39` | **done, verified, uncommitted** |
| Task 6 | `CC-34`, `CC-39` | **done, verified, uncommitted** |
| Task 7 | `CC-36`, `CC-39` | **done, verified, uncommitted** |
| Task 8 | `CC-37`, `CC-38` | **done, verified, uncommitted** |
| Task 9 | gateway envelope | **done, verified, uncommitted** |
| Task 10 | `CC-34` (gateway side) | **done, verified, uncommitted** |
| Task 11 | `CC-35` (gateway side) | **done, verified, uncommitted** |
| Task 12 | `CC-36` (gateway side) | **done, verified, uncommitted** |
| Task 13 | `CC-50` verification + docs | **done, verified, uncommitted** |

## Task 1 — full record

**Commit:** none yet. `git status` shows exactly six files modified, uncommitted:
`backend/src/CustomerSupport.Infrastructure/Notifications/{Email,Sms,WhatsApp}NotificationChannelSender.cs`,
`backend/tests/CustomerSupport.Tests/Integration/GatewayTestData.cs`,
`backend/tests/CustomerSupport.Tests/Integration/WhatsAppOutboundReplyTests.cs`,
`backend/tests/CustomerSupport.Tests/Unit/Notifications/WhatsAppNotificationChannelSenderTests.cs`.

**What was actually wrong (`CC-51`, spec `A19`):** `DatabaseExternalApiProvider.MapToConfig`
(`ExternalApis/Providers/DatabaseExternalApiProvider.cs:96-101`) already decrypts every stored
credential before handing the config to a caller. Every one of the three HTTP channel senders then
called `_secretProtector.Unprotect(...)` on that already-plaintext value inside `ApplyAuth`.
`DataProtectionSecretProtector.Unprotect` delegates to `IDataProtector.Unprotect`, which throws on
input that isn't a valid protected payload — and because `ApplyAuth` runs outside the senders' own
retry `try` block, the exception escaped `SendAsync` entirely and was swallowed by
`NotificationGateway.cs:93-99`, which logged it and recorded `DELIVERY_FAILED`. No outbound Email,
SMS, or WhatsApp send has ever left the process for any `AuthType` other than `None`. The existing
unit tests never caught this because they inject `IdentitySecretProtector`, whose `Unprotect` is a
no-op — the double-unprotect is invisible at unit level by construction.

**Fix:** removed the `ISecretProtector` field/constructor parameter from all three senders; `ApplyAuth`
now uses `auth.Value`/`auth.Token` as given, with a `when !string.IsNullOrWhiteSpace(...)` guard so an
absent credential is skipped rather than sent as a malformed header.

**Two further defects found during execution, neither anticipated by the plan:**

1. **`GatewayTestData.SeedWhatsAppGatewayAsync` seeded the wrong config field.** It set
   `authType: "Bearer", authValue: ...`, but `ApplyAuth`'s `Bearer` branch reads `auth.Token`, not
   `auth.Value` — so even with the double-unprotect fixed, the outbound Authorization header was
   built from an empty string. Root-caused by adding a temporary diagnostic (`ILoggerProvider`
   writing to `Console`, plus a `Console.WriteLine` trace directly in
   `RecordTicketMessageCommandHandler`) after `WhatsAppOutboundReplyTests.CC10_WhatsAppReply...`
   stayed red with `dispatchResult.Succeeded=False results=[WhatsApp:False:NOTIFICATION_DELIVERY_FAILED]`
   and no exception logged anywhere — proving the HTTP call was now actually being attempted and
   failing non-transiently rather than throwing. Both diagnostics were removed before this record was
   written; `git diff` on `CrmApiFactory.cs` and `RecordTicketMessageCommandHandler.cs` is empty.

2. **The seeded base URL pointed at the stub's root, not its mapped route.**
   `WhatsAppOutboundReplyTests.InitializeAsync` called
   `_factory.SeedWhatsAppGatewayAsync(_stub.BaseUrl)` — the bare host — but `StubGatewayServer` maps
   its handler at `/messages`, and the sender POSTs straight to `config.BaseUrl` with no path
   composition of its own. Every outbound request 404'd at ASP.NET's routing layer before the mapped
   endpoint ever ran; a 404 is non-transient, so the sender made exactly one attempt and the stub
   recorded zero received bodies — the exact symptom (`"found 0"`) from the very first run. Fixed by
   composing the path at the one call site that dereferences it
   (`$"{_stub.BaseUrl.TrimEnd('/')}/messages"` in `WhatsAppOutboundReplyTests.cs`), not inside
   `GatewayTestData`, because `WhatsAppWebhookTests` passes its own complete fake URL to the same
   helper and never dereferences it.

3. **Fixing (2) broke `WhatsAppWebhookTests.CC8_SignedWebhook...` and `CC9_RetriedDelivery...`**,
   which were passing before this task and were only caught by running the *full* suite and diffing
   named failures against a baseline (see below) — a single before/after count comparison could not
   have caught this, since the counts moved in ways that happened to look plausible on their own.
   Cause: `MetaSignatureVerifier.cs:41` reads the webhook-signing secret from `config.Auth.Value`,
   a *different* field from the outbound sender's `Auth.Token`. The test fixture uses one secret
   constant (`WhatsAppAppSecret`) for both roles, so the seed must populate **both** fields with the
   same protected value. Fixed in `GatewayTestData.SeedWhatsAppGatewayAsync`.

**Verification method — name-level diff, not count comparison.** A raw "Failed: N" count cannot
distinguish "fixed one test, broke another" from "no net change" — which is exactly what happened
here on the first pass (Task 1's fix alone left the total failure count *unchanged* relative to
baseline, because one fix and one regression cancelled out numerically). To get a real answer:

1. Backed up this task's six files, reverted them to `HEAD` with `git checkout --`.
2. Ran the full suite at `HEAD`: **`Failed: 57, Passed: 730, Total: 787`**
   (`dotnet test CustomerSupport.slnx --logger "trx;LogFileName=baseline-suite.trx"`).
3. Restored the six files from backup.
4. Ran the full suite with every fix in place: **`Failed: 56, Passed: 732, Total: 788`**.
5. Extracted the exact `[FAIL]` test names from each run's TRX (`RunInfo` elements with
   `outcome="Error"` — these matched dotnet's own console-reported failure count exactly, `57` and
   `56`; the `UnitTestResult` `outcome="Failed"` count in the same file was `58`/`59` and did not
   reconcile with the console total, so it was not used) and diffed the two name sets directly.

**Result:**
- Fixed (failing at baseline, passing after): exactly one —
  `WhatsAppOutboundReplyTests.CC10_WhatsAppReply_RecordsOutboundMessageAndDispatchesToTheGateway`.
- New regressions (passing at baseline, failing after): **none** — the set is empty.
- The 56 remaining failures are byte-for-byte identical between the two runs.

**The 56 pre-existing failures are out of this task's scope and were not investigated further.**
They span areas with no relationship to channel senders — `PermissionTests`, `AuditLogEndpointTests`
(`403 Forbidden` on authenticated calls), `TicketLifecycleEndpointTests`, and even pure `Domain`/
`Validators` unit tests with no HTTP or database dependency (`TicketStatusTests`, `BranchTests`,
`CustomerTests`). This pattern — and the `403`s specifically — is consistent with the sandbox
permission/identity-seeding defect already on record in
[`EPIC-09-US-806-feat-34-permissions-workbench`'s README](../EPIC-09-US-806-feat-34-permissions-workbench/README.md),
reproduced there on an untouched, pre-existing test in isolation. Not re-diagnosed here; recorded so
the next person does not re-attribute it to this feature.

**Final confirmation run, after both extra fixes, scoped to every WhatsApp test:**

```
Test run for CustomerSupport.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    15, Skipped:     0, Total:    15, Duration: 28 s
```

(8 `WhatsAppNotificationChannelSenderTests` including the new `CC51_...` test, 2
`WhatsAppOutboundReplyTests`, 5 `WhatsAppWebhookTests` — `CC8`, `CC9`, `CC5_Unsigned...`, and the
2-case `CC5_WrongSignature...` theory.)

**Not committed.** Explicit instruction this session was to implement and verify without committing.
The commit message drafted in the plan (Task 1, step 6) still applies but should be extended to
mention the `Auth.Value`/`Auth.Token` split and the stub-URL path fix, since all three landed in the
same change.

## Task 2 — full record

**Commit:** none yet. `git status` shows two new files
(`backend/src/CustomerSupport.Domain/Common/ChannelNames.cs`,
`backend/tests/CustomerSupport.Tests/Unit/Domain/ChannelNamesTests.cs`) and five modified
(`TicketMessage.cs`, `RecordTicketMessageCommandValidator.cs`,
`IngestInboundChannelMessageCommandValidator.cs`, `CreateTicketCommandValidator.cs`,
`Application/Channels/Contracts.cs`'s doc comment), on top of Task 1's six.

**What it does:** collapses four independently-maintained channel-name lists that had already drifted
apart (`TicketMessage` permitted 7 values, `RecordTicketMessage`'s validator only 6 — missing
`Portal` — the ingest validator only 3, and `CreateTicket`'s `Source` set a sixth combination) into
one `ChannelNames` static class in `Domain/Common`, with `All`/`Inbound`/`TicketSources` covering the
three distinct roles the old lists played. `Email` becomes a legal inbound channel for the first
time (needed by plan 2's `CC-42`).

**No behavioural change** — every consumer's actual set of accepted values is unchanged; this is a
pure extract-constant refactor. Confirmed by running the exact test files that assert those
boundaries (`TicketMessageTests`, `CreateTicketCommandValidatorTests`,
`RecordTicketMessageCommandValidator`'s coverage, `IngestInboundChannelMessageCommandValidator`'s
allow-list, plus the new `ChannelNamesTests`): **29/29 pass**.

**One incidental failure surfaced when the filter was run wider than planned** (adding
`+IngestInboundChannelMessage`, 37 tests total):
`IngestInboundChannelMessageTests.CC2_MessageAfterResolution_StartsANewTicket`, error `"Ticket
'TKT-001004' cannot be resolved without a resolution code and notes."` — a FEAT-32 resolution-
discipline rule, unrelated to channel names. Confirmed pre-existing (not caused by this task) by
grepping the exact test name against both `/tmp/baseline-failed-names.txt` and
`/tmp/final-failed-names.txt` — the two name lists produced during Task 1's full-suite verification —
and finding it present in both.

**Verification scope, and why a full-suite name-diff was not repeated:** per instruction this
session, Task 1's expensive full-suite-plus-diff verification (~9 minutes each of two runs) was not
repeated for this task. This is a mechanical extract-constant refactor — every list's actual values
are unchanged, only their storage location moves — a materially lower-risk shape than Task 1's
behavioural fix, which is what justified the heavier method there. The one incidental failure this
task did surface was checked directly against Task 1's already-established 56-name pre-existing
baseline rather than by re-running the full suite from scratch.

**Not committed**, same instruction as Task 1.

## Task 3 — full record

**Commit:** none yet. `git status` shows five new files
(`ChannelOptions.cs`, `ChannelMockGuard.cs`, `MockRoutingExternalApiConfigurationProvider.cs`,
`MockRoutingExternalApiConfigurationProviderTests.cs`, plus Task 2's `ChannelNames.cs`/
`ChannelNamesTests.cs` already counted) and modifications to `ServiceCollectionExtensions.cs` and
both hosts' `appsettings.json`, on top of Tasks 1–2's files.

**What it does:** the mock/real toggle exactly as designed — `MockRoutingExternalApiConfigurationProvider`
decorates `DatabaseExternalApiProvider` behind the single `IExternalApiConfigurationProvider` port
every sender and the inbound signature verifier already read through, so no sender, handler, or
controller has any awareness that mocks exist. `Channels:UseMocks` (default `false`) controls it;
`ChannelMockGuard.Validate` throws at startup if it is ever `true` under `Production`. Registered in
`ServiceCollectionExtensions.cs`, config keys added to both `InternalApi` and `ExternalApi`
`appsettings.json`.

**Verification:** the plan's own 9 tests (`CC30`–`CC33`) pass, and a wider targeted run spanning
everything Tasks 1–3 touch (`MockRoutingExternalApiConfigurationProviderTests`, `ChannelNamesTests`,
`WhatsAppNotificationChannelSenderTests`, `WhatsAppOutboundReplyTests`, `WhatsAppWebhookTests`,
`TicketMessageTests`, `CreateTicketCommandValidator*`, `RecordTicketMessage*`) is **53/53 green**.
The full-suite name-diff was not repeated, per the same reasoning as Task 2 — this task's shape
(one new decorator behind an existing interface, activated only when a flag defaults to off) carries
materially lower risk than Task 1's behavioural fix.

**One real scare during this step, resolved by direct isolation rather than assumption.** A broader
ad hoc test filter that happened to include `TicketMessagesEndpointTests` showed 9 failures
(`Sequence contains no elements`, from a test helper's own `GET /api/Categories` call returning
empty). Given this task's change touches `ServiceCollectionExtensions.cs` — the shared DI
bootstrapping every integration test's host goes through — this looked exactly like the shape of bug
a wrong registration would cause. It was not assumed innocent or assumed guilty; it was isolated:

1. Reverted only `ServiceCollectionExtensions.cs` to `HEAD` → identical 9/13 failure. Ruled out this
   task's registration change specifically.
2. Reverted **every** file from Tasks 1 and 3 to `HEAD` simultaneously (Task 2's
   `CreateTicketCommandValidator.cs` and `ChannelNames.cs` had to stay — `CreateTicketCommand.cs`,
   untouched by any of these three tasks, already dropped its `Priority` property in FEAT-32's own
   uncommitted work, so pure `HEAD`'s validator referencing `Priority` does not compile against it)
   → identical 9/13 failure again.

`TicketMessagesEndpointTests.cs` is itself one of the roughly fifty files already modified,
uncommitted, on this branch before this session started (FEAT-32's in-progress resolution/impact/
tags/links work). The failure is that work's, not this feature's, established by direct repeated
proof rather than by pattern-matching against the FEAT-34 sandbox-defect precedent the way Task 1's
56 pre-existing failures were. Not investigated further — out of this task's scope.

**A process cost worth recording:** the first isolation attempt (reverting `ServiceCollectionExtensions.cs`
alone via `git checkout --`, then later reverting the fuller set) required backing up and restoring
14 files by hand, including recreating five new files from memory after an `rm -f` swept them along
with files that needed to stay reverted. A background full-suite run left over from Task 2's
verification (started before the "skip full-suite reruns" instruction landed, and still running)
also collided with this task's own build — a `testhost.exe` process held a file lock on shared
build output, and the build failed with `MSB3027`/`MSB3021` copy errors that looked like a build
problem but were pure resource contention. Diagnosed by checking the log tail for a stale
in-progress run and resolved by terminating that specific process (`taskkill`) rather than the
build.

**Not committed**, same instruction as Tasks 1–2.

## Task 4 — record (brief; user asked to move faster)

New `Notifications/Channels/ChannelHttpSender.cs` (abstract base: config lookup, client, auth,
retry, result mapping). All three senders reduced to ~30-line adapters supplying only
`BuildContent`/`ReadProviderMessageIdAsync`/`ConfigName`/`SupportedChannel`. Removed now-unused
`IdentitySecretProtector` from the WhatsApp test file (Task 1 already dropped the constructor
parameter it stood in for). `CC49_ContentIsRebuiltPerAttempt` passed even before the base existed —
`StringContent` is safe to resend in .NET, so that specific predicted bug doesn't reproduce; the base
is still correct and still fixes the real bug (fabricated provider ids). 20/20 targeted tests green
(`WhatsAppNotificationChannelSenderTests`, `NotificationGatewayTests`, `WhatsAppOutboundReplyTests`,
`WhatsAppWebhookTests`). Not committed.

## Task 5 — record (brief)

`WhatsAppNotificationChannelSender.ReadProviderMessageIdAsync` now reads `messages[0].id` from
Meta's response instead of returning null (Task 4's stub) / fabricating a guid (pre-Task-4). Two new
tests (`CC35_ProviderMessageId_IsReadFromMetasResponse`, `CC39_PayloadIsExactlyMetaCloudApisShape`)
plus the existing 9 = 11/11 green. Re-ran `WhatsAppOutboundReplyTests`/`WhatsAppWebhookTests` (7/7)
to confirm the real stub's empty-body 200 still resolves to a null id via the existing
try/catch — no regression. Not committed.

## Tasks 6–8 — records (brief; user asked to move faster)

**Task 6 (Email/SendGrid):** new `EmailNotificationChannelSenderTests.cs`; adapter now builds
SendGrid v3's `personalizations`/`from`/`subject`/`content` shape and reads the id from the
`X-Message-Id` response header (202, empty body). Constructor gained `IOptions<ChannelOptions>` for
`EmailFrom` — resolves automatically, `Configure<ChannelOptions>` was already registered in Task 3.
9/9 green (`EmailNotificationChannelSenderTests`, `TicketCreatedNotificationTests`, `OtpRequest*`).
`SlaNotificationTests`' 3 failures cross-checked against Task 1's baseline — pre-existing, unrelated.

**Task 7 (SMS/Twilio):** new `SmsNotificationChannelSenderTests.cs`; adapter now form-encodes
`To`/`From`/`Body` (the one non-JSON channel) and reads `sid` via the base's
`ReadJsonStringAsync` helper. `System.Web.HttpUtility` compiled with no extra framework reference.
2/2 new, 19/19 across all three sender files. Not committed.

**Task 8 (429 retry):** two tests added to the Email file (`CC38_TooManyRequests...`,
`CC37_BadRequest...`); WhatsApp already had an equivalent `CC7_PermanentFailure_IsNeverRetried`, so
no duplicate added there. One-line fix: `TooManyRequests` added to `ChannelHttpSender.IsTransient`.
18/18 across all three sender files. Not committed.

## Tasks 9–12 — gateway side, records (brief; done together since 9/10 are plan-coupled)

**Task 9 (envelope):** `middlewares/gateway-handler.js` now recognises a model returning
`{ $response: true, status, headers, body }` and answers with that exact status/headers/body
(`body: null` → `res.status(status).end()`, no JSON serialisation of `null`). Anything else — every
existing model — keeps the historical `200 + JSON`, verified by the "legacy sms route" check.

**Task 10 (SendGrid):** `models/SendGridGatewayModel.js` — `POST /mock/sendgrid/v3/mail/send`,
`202` + `X-Message-Id` header + empty body on success, SendGrid's real `errors[]` envelope on
validation/permanent/transient failure.

**Task 11 (Meta WhatsApp):** `models/MetaWhatsAppGatewayModel.js` —
`POST /mock/meta/v18.0/:phoneNumberId/messages`, `200` with `messages[0].id` as a `wamid.`-prefixed
value, Meta's real `error.{message,type,code,fbtrace_id}` envelope on failure.

**Task 12 (Twilio):** `models/TwilioGatewayModel.js` —
`POST /mock/twilio/2010-04-01/Accounts/:accountSid/Messages.json`, form-encoded request (Twilio is
the one non-JSON channel), `201` with a `sid`, Twilio's real `{code,message,more_info,status}`
envelope on failure.

Shared: `behaviors/provider-failure-rules.js` — deterministic (not random) permanent/transient
triggers keyed by recipient (`permanent-fail@mock.test`/`+19995550000` → permanent;
`transient-fail@mock.test`/`+19995550001` → fails twice, then succeeds), so the backend's bounded
retry policy is provable end-to-end. `mocks/providers/history.json` (empty array) backs all three
under the shared `providers-history` mock key. All three registered in `models/ServiceRegistry.js`.

**Verified live, not just read**, per this project's own rule about claiming things without running
them:
- Started the real Node server (`node server.js`), confirmed all three routes registered in its own
  startup log, confirmed `providers-history` mock loaded.
- `npm run test:envelope` (extended beyond the plan's single-provider script to cover all three
  providers plus the legacy-route regression check) — **8/8 PASS**.
- Hit the deterministic failure triggers directly with `curl`: `permanent-fail@mock.test`-equivalent
  phone on the SendGrid route → `400`; the WhatsApp transient recipient → `503`, `503`, then `200`
  on the third call — the exact shape a 3-attempt retry policy needs to prove recovery.
- Stopped the server cleanly (found its actual PID via `netstat -ano | grep :3001`, not a guess
  among the many unrelated `node.exe` processes already running on this machine, then `taskkill`).

Merged the plan's per-task envelope-check script additions into one `scripts/test-response-envelope.js`
written once (Task 9) and extended in place, rather than three near-duplicate files — the plan's own
Task 9 step 4 already anticipated the script would be revisited once Task 10 landed. All not committed.

## Task 13 — full record

**Files:** `cms-integration-gateway/.env.example`, `cms-integration-gateway/config.js`,
`cms-integration-gateway/CLAUDE.md`, repo-root `CLAUDE.md`, this README. None committed, same
instruction as every other task.

**Step 1 (config):** added `CALLBACK_BASE_URL` (default `http://localhost:5095`) and
`WEBHOOK_SECRET` (default `dev-only-channel-webhook-secret`) to both `.env.example` and `config.js`'s
`config` object. Not consumed by anything yet — plan 2 (inbound) is what reads them.

**Step 2 — the toggle proved end-to-end, by hand, not just at unit-test level:**
- Started the real gateway (`npm start`, port 3001) and the real `InternalApi` host with
  `Channels__UseMocks=true`, `ConnectionStrings__DefaultConnection` pointed at the real LocalDB,
  console logging enabled.
- Logged in as the seeded `admin@cce-platform.com` (the seeded `superadmin@support.local` /
  `Support@123456` fixture from `IdentitySeeder.SeedRoleUsersAsync` rejected with `ERR023` —
  not investigated further, out of scope; the default admin fixture from `SeedDefaultAdminAsync`
  worked).
- Created a ticket against a seeded customer who actually has a phone number (`Layla Haddad`,
  `+20 100 000 0000`) — the first customer tried had `phone: null` and produced
  `[WRN] Outbound WhatsApp reply ... had no customer phone to send to`, a real and correct guard,
  not a bug; switching customers was the fix.
- `POST /api/Tickets/{id}/messages` with `{"direction":"Outbound","channel":"WhatsApp","body":"testing the mock"}`.
- **Observed gateway log:** `[2026-09-02T12:08:27.928Z] POST /mock/meta/v18.0/100000000000000/messages`
  with body `{"messaging_product":"whatsapp","to":"+20 100 000 0000","type":"text","text":{"body":"testing the mock"}}`.
- **Observed `NotificationDeliveries` row** (`sqlcmd` against the real LocalDB):
  `Channel=WhatsApp, Status=Delivered, ProviderMessageId=wamid.NmFjZDUwZGQtMDU4NC00NTM2LTlkOGUtYjk3MGRhY2JjY2M2`
  — the `wamid.` prefix is Meta's real id shape, proving the sender read it from the mock's response
  body rather than fabricating one.

**Step 3 — the production guard, with one false start worth recording.** The first attempt
(`ASPNETCORE_ENVIRONMENT=Production dotnet run ...`) started successfully and did **not** throw —
looked like the guard was broken. Root cause: `dotnet run` applies `Properties/launchSettings.json`'s
environment variables *on top of* the invoking shell's, and the default profile pins
`ASPNETCORE_ENVIRONMENT=Development`, silently overriding the inline shell variable. Confirmed via the
log line `Hosting environment: Development` despite the shell explicitly exporting `Production`. Fixed
by adding `--no-launch-profile`; the guard then fired exactly as designed:
```
Unhandled exception. System.InvalidOperationException: Channels:UseMocks must not be true when the
environment is Production. Remove the setting or point the channel gateways at real providers.
   at CustomerSupport.Infrastructure.ServiceCollectionExtensions.RegisterPlatformInfrastructure(...)
```
This was a test-harness artifact, not a code defect — `ChannelMockGuard`/its call site in
`ServiceCollectionExtensions.cs:76-82` needed no change.

**Step 4 — `CC-50`, proved by full-suite run with nothing on port 3001:**
- Confirmed both the gateway and `InternalApi` processes were stopped and ports 3001/5074 clear
  (`netstat`) before starting the run.
- `dotnet test CustomerSupport.slnx --logger "trx;LogFileName=cc50_no_gateway.trx"`:
  **`Failed: 56, Passed: 754, Skipped: 0, Total: 810, Duration: 9m 27s`.**
- The failure **count** (56) matches Task 1's already-established "fixed" baseline exactly; passed/total
  grew by the 22 tests Tasks 2–12 added. Per this project's own rule against trusting a count alone
  (Task 1 already found a fix and a regression can cancel out numerically), the 56 **names** were
  extracted from the TRX (`<RunInfo outcome="Error">` elements — the same method Task 1 validated
  against the console's own reported count) and diffed byte-for-byte against Task 1's saved
  `/tmp/final-failed-names.txt`: **identical, zero difference.** `CC-50` holds — no test in the suite
  depends on the mock gateway being reachable, and Tasks 1–12 introduced no regressions beyond the
  pre-existing 56 already on record.

**Step 5 (docs):** added the "Provider-faithful channel mocks (FEAT-35)" section (the three mock
routes, the deterministic failure-trigger table, the envelope contract) to
`cms-integration-gateway/CLAUDE.md`'s "Current Services", and the two new rows (mock gateway command,
`Channels:UseMocks` toggle) to the repo-root `CLAUDE.md`'s Commands table.

**Step 6:** this record (6a). Commit (6b) not executed — explicit no-commit instruction for the whole
implementation pass; the commit message drafted in the plan still applies when that instruction lifts.

**Plan 1 is now fully implemented and verified, end to end, uncommitted.** Plan 2 (inbound: SMS, email,
web-form — live-chat and the abandoned-session job deferred per instruction) has its own spec
amendment and plan still to come.
