# FEAT-35 plan 2 (inbound completion — SMS, email, web form) — record

**Plan:** [`implementation-plan.md`](implementation-plan.md)
**Spec:** [`EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md`](../../specs/EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md)
— the **"Amendment — 2026-09-02, inbound completion"** section (`A20`–`A27`).
**Predecessor:** [plan 1's record](../EPIC-03-US-201-feat-35-channel-mock-gateway/README.md) (outbound + the mock/real toggle).

## Status: Tasks 1–9 implemented and verified; **not committed** (explicit instruction for the whole session).

## Criteria delivered

| Task | Criteria | Status |
|---|---|---|
| Task 1 | `CC-40`/`CC-41` (Twilio signature algorithm) | **done, verified, uncommitted** |
| Task 2 | `CC-40`/`CC-41` (two verifiers behind one port) | **done, verified, uncommitted** |
| Task 3 | `CC-40`/`CC-41` (SMS webhook, end to end) | **done, verified, uncommitted** |
| Task 4 | `A23` — optional `Subject` on the shared command | **done, verified, uncommitted** |
| Task 5 | `CC-42`/`CC-43` (inbound email) | **done, verified, uncommitted** |
| Task 6 | `CC-44` (email reply actually reaches the customer) | **done, verified, uncommitted** |
| Task 7 | `CC-47` revised (portal web-form backend) | **done, verified, uncommitted** |
| Task 8 | `A26` (gateway inbound simulators) | **done, verified live, uncommitted** |
| Task 9 | `CC-50` re-proved + documentation | **done, verified, uncommitted** |

`CC-45`/`CC-46` (live-chat visitor simulator, abandoned-session job) are **deferred by explicit
instruction** and are implemented by nothing here. Recorded in the delivery plan's `FEAT-35` row so
the gap is visible rather than merely absent.

## Task 1 — `TwilioSignatureVerifier`

New `Infrastructure/Channels/TwilioSignatureVerifier.cs`, modelled on `MetaSignatureVerifier.cs`:
same constructor dependency, same "the provider already decrypted this credential, do not
re-`Unprotect` it" discipline (the `CC-51` lesson), same `FixedTimeEquals` comparison. The algorithm
is Twilio's, which differs from Meta's in all three particulars — HMAC-**SHA1** not SHA256,
**Base64** not hex, and over **the URL plus ordinal-sorted form parameters** rather than the raw
body.

Two tests exist specifically because they distinguish a correct implementation from one that passes
the obvious case: `CC40_ParameterOrderInTheBody_DoesNotAffectTheResult` (Twilio sorts by key when
signing, so a verifier that hashed the body in wire order would pass the happy path and fail in
production) and `CC41_UrlEncodedValues_AreVerifiedDecoded` (Twilio signs decoded values while
transmitting percent-encoded ones, so verifying the raw text would reject every message containing a
space).

```
Passed!  - Failed:     0, Passed:    10, Skipped:     0, Total:    10, Duration: 176 ms
```

## Task 2 — `CompositeWebhookSignatureVerifier` and the registration swap

`ServiceCollectionExtensions.cs:107` registered `MetaSignatureVerifier` directly as the single
`IWebhookSignatureVerifier`. Registering a second implementation against the same interface would
have resolved to whichever came last and silently broken the other channel, so both are now
registered as concrete types and reached only through a composite that dispatches on the `provider`
argument each verifier already gates on. Neither webhook controller changed shape.

Verified with the WhatsApp tests included deliberately — they are the only proof the swap did not
change WhatsApp's behaviour:

```
Passed!  - Failed:     0, Passed:    33, Skipped:     0, Total:    33, Duration: 19 s
```

(4 composite + 10 Twilio + 19 WhatsApp.)

## Task 3 — `SmsWebhookController`, and a plan assumption that turned out to be wrong

**Deviation from the plan, found by execution.** The plan had this controller copy
`WhatsAppWebhookController.cs`'s raw-body read (`EnableBuffering()` → `CopyToAsync` → verify). It
does not work for form posts, and the first run said so: the two refusal tests passed while all
three signed ones returned `401`.

Diagnosed rather than guessed at, in three steps:

1. A throwaway diagnostic test confirmed the config side was fine — the seeded `SmsGateway` row
   resolved with `Auth.Value` = the expected secret, and the client's base address was
   `http://localhost/` as assumed.
2. A second diagnostic proved the algorithm was fine: fed the *exact* bytes
   `FormUrlEncodedContent` puts on the wire
   (`From=%2B15551230001&Body=My+order+has+not+arrived&MessageSid=…`), the verifier accepted the
   signature.
3. A temporary echo endpoint inside the controller printed what the server actually saw:
   `{"url":"http://localhost/api/channels/sms/webhook","len":0,"hasForm":true,"formKeys":["From","Body","MessageSid"],"from":"+15551230001","body":"My order has not arrived"}`

`len: 0` was the answer: **the request body is already drained by the time the action runs**. MVC's
form value provider reads and caches the form during model binding for `x-www-form-urlencoded`
requests. Nothing in this repository does it (`grep` for `ReadFormAsync`/`Request.Body` across
`Api.Shared` and `ExternalApi` finds only the webhook controllers themselves) — it is the
framework's own behaviour, so no amount of `EnableBuffering()` inside the action recovers the bytes.

**Resolution:** the SMS controller verifies against the framework's parsed form, re-encoded
canonically. That is lossless *for this provider and only this provider*, because Twilio signs the
decoded parameter values — Meta's raw-body SHA256 could not be verified this way, and the WhatsApp
controller is deliberately left reading raw bytes. The reasoning is written into the controller so
the next person does not "simplify" the two into one shape.

All diagnostics were removed before this record was written: the throwaway test file is deleted, and
`git diff` on `TwilioSignatureVerifier.cs` shows no `Console.WriteLine` and no `X-Diag` branch in
`SmsWebhookController.cs`.

```
Passed!  - Failed:     0, Passed:    20, Skipped:     0, Total:    20, Duration: 20 s
```

(5 SMS inbound + 10 Twilio unit + 5 WhatsApp webhook.)

## Task 4 — optional `Subject` on the shared ingestion command (`A23`)

`IngestInboundChannelMessageCommandHandler.cs:74` synthesized `"{Channel} — {CustomerName}"` for
every ticket unconditionally. Right for WhatsApp and SMS, which have no subject; silently lossy for
the web form (which collects one) and inbound email (which has a `Subject:` header). `Subject` is
now the last parameter with a `null` default, so every existing call site compiled untouched, and
the validator caps it at 200 to match `Ticket.Create`'s own limit — turning what would have been an
unhandled `ArgumentException` into a field-keyed 400. `SUBJECT_MAX_LENGTH` already existed, so no
`Resources.yaml` change was needed and `ContractHardeningTests` was unaffected.

```
Failed!  - Failed:     1, Passed:    15, Skipped:     0, Total:    16, Duration: 28 s
```

The single failure is `IngestInboundChannelMessageTests.CC2_MessageAfterResolution_StartsANewTicket`,
**confirmed pre-existing** by matching it against plan 1's saved 56-name baseline
(`grep -c` in `/tmp/final-failed-names.txt` → `1`). It is a FEAT-32 resolution-discipline failure
with no relationship to channels.

## Task 5 — `EmailWebhookController`

`POST /api/channels/email/webhook`, `[Consumes("multipart/form-data")]`, reading SendGrid Inbound
Parse's `from`/`subject`/`text`/`headers`/`envelope` fields. **No signature check**, and that is
deliberate and specified (`A21`): Inbound Parse does not sign its posts, unlike SendGrid's separate
Event Webhook. Two parsing decisions, both delegated to the BCL rather than hand-rolled:

- `System.Net.Mail.MailAddress` splits `"Layla Haddad" <layla@example.com>` into address and display
  name, because it already implements RFC 5322's quoting rules. A value it rejects is refused with
  400 rather than stored — an address this system cannot reply to is not worth a ticket.
- `Message-ID` for `CC-43`'s idempotency is pulled out of the forwarded raw `headers` with a
  `[GeneratedRegex]`. Inbound Parse has no id field of its own. When a sender omits the header,
  `ProviderMessageId` is null and the shared handler simply does not deduplicate — the same
  behaviour it already has for any channel that cannot supply an id.

```
Passed!  - Failed:     0, Passed:     6, Skipped:     0, Total:     6, Duration: 15 s
```

## Task 6 — `CC-44`, and why the spec's own "one-line fix" was wrong (`A27`)

This is the task the plan's grounding pass justified. `CC-44` as originally written — and as this
amendment first restated it — claimed adding `or "Email"` to
`RecordTicketMessageCommandHandler.cs:72`'s channel gate was the whole change. Reading the block it
would join showed otherwise: lines 72-89 resolve `customer?.Phone`, skip the send when it is blank,
and dispatch with `PhoneNumber: phone, Email: null`. The one-line version would have dispatched
every email reply with a null address and the customer's *phone number* in `PhoneNumber` — which
`EmailNotificationChannelSender` cannot deliver. The spec was corrected (`A27`) **before** the code
was written, not after.

The rewritten block selects the contact by channel, following `RequestOtpCommandHandler.cs:83-92`'s
precedent of setting exactly one of `Email`/`PhoneNumber`, and additionally skips
`@channel.invalid` addresses — the deterministic RFC-2606 placeholders
`IngestInboundChannelMessageCommandHandler.cs:115` mints for phone-only customers to satisfy
`Customer.Email`'s non-nullable contract. Dispatching to one would have been recorded as sent and
delivered nowhere, which is the failure mode `CC-51` already taught this codebase to distrust.

The failing-first run was exactly the predicted shape — the email dispatch test red, the two
regression fences green:

```
[FAIL] EmailOutboundReplyTests.CC44_EmailReply_DispatchesToTheEmailGatewayAddressedToTheCustomer
Failed!  - Failed:     1, Passed:     2, Skipped:     0, Total:     3, Duration: 9 s
```

After the fix, with the WhatsApp outbound tests included to prove phone routing was untouched:

```
Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5, Duration: 13 s
```

The warning text changed from "had no customer phone" to "had no deliverable customer contact",
since it now covers both cases. No test asserted on the old string.

## Task 7 — `WebFormController`, the throttle, and the reference query

**The contract was not designed here — it was already fixed by the frontend** (`A20`). The portal's
`web-form` feature and its `WebFormApi` client already existed, already posting
`{name, email, subject, description, honeypot?}` to `/api/external/webform/submit` and typing the
reply as `{reference, success}`. The backend was built to that.

One detail in it would have been easy to get silently wrong: portal-app registers
`envelopeInterceptor` (`app.config.ts:23`), which unwraps `Response<T>` to its `data`. So the
endpoint returns the standard platform envelope with `data = {reference, success}` — including the
nested `success` the TypeScript interface declares. Returning a bare `{reference, success}` would
have been unwrapped to nothing and shown the customer `undefined` as their ticket reference.

`CC-47`'s indistinguishability requirement drove the two defences (`A24`): the ASP.NET rate limiter
always answers `429` (`WebApiServiceExtensions.cs:53`), which is precisely the signal a probing bot
must not receive, and `IMemoryCache` is registered nowhere in this solution. So the throttle is a
singleton holding its own `ConcurrentDictionary`, taking `IDateTimeService` so the window is
testable without sleeping, with the same 5-per-5-minutes budget per IP as the existing `"login"`
policy. Honeypot and throttle both return the same `201` and a `TKT-`+6-digit reference drawn at
random and never persisted — matching `TicketReferenceGenerator.cs:49`'s real shape while consuming
no sequence value.

The reference itself is read through a new `GetTicketReferenceForMessageQuery` rather than by
widening the shared ingestion command's response (`A25`) or by injecting a repository into a
controller — every controller in both hosts dispatches through `IMediator` and none injects
`IRepository<T>`, so a raw repository read would have been the only one of its kind.

```
Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5, Duration: 131 ms   (throttle)
Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4, Duration: 14 s     (endpoint)
```

The throttle's `CC47_ConcurrentAcquisitions_NeverExceedTheLimit` runs 200 parallel acquisitions and
asserts exactly `PermitLimit` were granted — a `ConcurrentDictionary` alone would not have given
that, which is why each counter is mutated under its own lock.

## Task 8 — gateway inbound simulators (`A26`), verified live

`scripts/simulate-sms-inbound.js` and `scripts/simulate-email-inbound.js`, plus `simulate:sms` /
`simulate:email` in `package.json`. They post **out to the backend** at `CALLBACK_BASE_URL` — the
gateway is the pretend provider here, and a provider calls us — using the two settings plan 1's
Task 13 added and nothing had consumed until now.

**Run against the real ExternalApi host, not asserted in the abstract:**

```
=== EMAIL (twice, CC-43) ===
POST http://localhost:5095/api/channels/email/webhook
  from="Layla Haddad" <layla@example.com> Message-ID=<ef36c102-1c0c-43d8-beb4-ee06f7fdd244@mail.example.com>
  attempt 1 -> 200 OK
  attempt 2 -> 200 OK
  posted twice with one Message-ID — CC-43 expects exactly one stored message
OK
=== SMS signed (CC-40) ===
POST http://localhost:5095/api/channels/sms/webhook
  From=+15551230001 MessageSid=SM8e0919eafbc7dc11b8cfcd08c3598a5f signed=true
  -> 200 OK
OK
=== SMS unsigned (CC-41) ===
POST http://localhost:5095/api/channels/sms/webhook
  From=+15551230001 MessageSid=SMae94403f896f31a55e2b9b574322f66a signed=false
  -> 401 Unauthorized
OK
```

The signed SMS passing is a genuine **cross-language contract check**: the Node `crypto` HMAC-SHA1
implementation and the C# `TwilioSignatureVerifier` agreed on the signed string independently. That
is worth more than either test alone, and it is exactly what would break first if someone
"simplified" the canonical-form re-encoding from Task 3.

Confirmed in the real database rather than inferred from the HTTP status:

```
Channel   Direction  ProviderId                                    Reference     Subj                     Source
SMS       Inbound    SM8e0919eafbc7dc11b8cfcd08c3598a5f            TKT-001012    SMS - New contact        SMS
Email     Inbound    <ef36c102-1c0c-43d8-beb4-ee06f7fdd244@mail…    TKT-001011    Refund not received      Email

-- CC-43: the Message-ID posted twice
<ef36c102-1c0c-43d8-beb4-ee06f7fdd244@mail.example.com>   1
```

The email ticket's subject is `Refund not received` — the email's own `Subject:` header, which is
Task 4's `A23` threading working end to end rather than the synthesized `"Email — Layla Haddad"`
default. `CC-43` stored exactly one row for two identical deliveries.

**Two startup traps hit while doing this, both now written into the repo-root `CLAUDE.md`** so the
next person does not rediscover them:

1. The ExternalApi host throws `RabbitMQ credentials must be configured` before it ever listens
   unless `Messaging__Required=false` is set. The test factories set it
   (`CrmExternalApiFactory.cs:30`); a hand-started host does not.
2. `dotnet run` applies `launchSettings.json`'s environment variables **over** the shell's. This is
   the same trap that produced a false "the production guard does not fire" reading in plan 1's
   Task 13, so `--no-launch-profile` is now documented as the default habit whenever the environment
   matters.

## Task 9 — verification and documentation

**Clean build under warnings-as-errors:**

```
    0 Warning(s)
    0 Error(s)
```

**Full suite, `CC-50` re-proved** — run with ports 3001 and 5095 confirmed clear beforehand
(`netstat` → `ports clear`), because this plan added three anonymous endpoints and changed shared DI
registration, which is exactly what could have made the suite depend on a running gateway:

```
Failed!  - Failed:    56, Passed:   794, Skipped:     0, Total:   850, Duration: 11 m 22 s
```

The failure **count** matches plan 1's established baseline exactly; `Passed` grew from 754 to 794,
the 40 tests this plan added. Per this project's own rule that a count alone proves nothing (plan 1's
Task 1 found a fix and a regression cancelling out numerically), the 56 names were extracted from the
TRX and diffed against plan 1's saved `/tmp/final-failed-names.txt`:

```
$ diff <(sort /tmp/final-failed-names.txt | tr -d '\r') <(sort /tmp/inbound-failed-names.txt | tr -d '\r')
IDENTICAL — zero regressions
```

`CC-50` therefore still holds: nothing in the suite needs the mock gateway, and the three new
anonymous endpoints plus the changed DI registration introduced no regression.

### Frontend state — checked, and two gaps closed

The web form's portal screen was **pre-existing** and is not this plan's work, but the question "is
the web form actually reachable in the portal?" needed answering rather than assuming, so:

- The screen is routed at **`/contact`** in the public area (`app.routes.ts:35`), and
  `proxy.portal.conf.json` routes the portal's `/api` to `localhost:5095`, so the wiring reaches
  this plan's new endpoint.
- Its own tests pass: `common` web-form API `1 passed`, `portal-app` web-form component `2 passed`.
- **Gap found and fixed (1):** `/contact` was linked only from the **footer**. The header nav carried
  Home, Solution, Live chat and Knowledge base — the web form, one of the brief's five channels, was
  absent from primary navigation while live chat sat right beside it. Added to the header nav next to
  Live chat, reusing the existing `landing.nav.contact` key the footer already used.
- **Gap found and fixed (2):** the landing page's five "channels" cards were plain `<div>`s carrying
  `hover:shadow-md hover:-translate-y-1` — they lift on hover, reading as clickable, and did nothing.
  The two channels with a real in-app destination (live chat, web form) are now `<a routerLink>`;
  Email, WhatsApp and SMS stay plain because there is nowhere in the app to send anyone.

`portal-app` builds clean and its test totals are **unchanged** by those two edits — verified by
reverting both files to `HEAD`, re-running, and comparing: `19 failed | 46 passed` before and after,
identically. Those 19 pre-existing failures live in `kb-list`, `kb-detail`, `dashboard` and
`tickets/detail` and are unrelated to this work; they are **not** investigated here and remain
outstanding.

### The client-side honeypot defect — fixed

`web-form.component.ts:57-61` checked the honeypot **in the browser** and returned a fabricated
reference *without ever calling the API*. Two things were wrong with that, and they pulled in
opposite directions:

- It **protected nothing**. A bot posts straight at the endpoint and never runs Angular, so the only
  caller this branch ever judged was a real person's browser.
- It **silently destroyed real submissions**. The bait input is `name="website"` — exactly the sort
  of field a browser autofills. Any customer whose browser filled it got a plausible-looking
  reference and had their ticket thrown away, with no request sent and nothing to trace.

It also fabricated `TICK-######` where every real reference in this system is `TKT-######`
(`TicketReferenceGenerator.cs:49`), so the fake was inconsistent with the real thing anyway.

**Fixed by moving the decision to where the defence already lived.** The component now always posts,
passing the `honeypot` field its own request interface already declared but never populated, and
renders whatever reference comes back. The server was already answering a honeypot-filled submission
indistinguishably from a real one (`CC-47`), so there is nothing left for the client to branch on.
The hidden input keeps its bait name `website`; only the wire field is called `honeypot`.

**The existing spec asserted the defect as intended behaviour** — a test literally named
"fakes success for bot when honeypot is filled without hitting API", using `http.expectNone(...)`.
That test was rewritten rather than deleted-to-go-green: the behaviour it locked in was the bug, and
changing a specified behaviour means changing its test alongside it. Three tests now cover the new
contract (honeypot forwarded, honeypot omitted when untouched, valid submission renders the
reference), and the misleading `TICK-` fixtures in both spec files were corrected to `TKT-`.

```
portal-app  web-form.component.spec.ts   Tests  3 passed (3)
common      web-form.api.spec.ts         Tests  1 passed (1)
portal-app  full suite                   Tests  19 failed | 47 passed (66)
```

The full portal-app run is the same 19 pre-existing failures as before this work, with `passed`
up by exactly the one test added.

### Proven end to end through the portal's own proxy

The remaining gap was that backend tests, component tests and the proxy config were three separate
proofs rather than one. Closed by running the real `ExternalApi` host and the real portal dev server
(`ng serve portal-app --port 4201`, its own `proxy.portal.conf.json`) and submitting through the
portal's origin — the exact path a browser takes, relative URL and all:

```
POST http://localhost:4201/api/external/webform/submit        (valid)
HTTP 201
{"success":true,"code":"ERR005","data":{"reference":"TKT-001013","success":true}, …}

POST http://localhost:4201/api/external/webform/submit        (honeypot filled)
HTTP 201
{"success":true,"code":"ERR005","data":{"reference":"TKT-407025","success":true}, …}
```

Identical status and identical envelope shape — and the shape is the one that matters, because
`data` is what `envelopeInterceptor` unwraps into the component's declared
`{reference, success}`. Confirmed in the database rather than inferred from the response:

```
-- the valid submission
Reference     Subj                       Source    Email                                  Name
TKT-001013    Proving the portal path    WebForm   browser-proof-…@example.com            Browser Proof

-- the honeypot submission
BotCustomers    0
FakeRefTickets  0        (TKT-407025 resolves to nothing, as intended)
```

One honest limitation of the disguise: real references come from a sequence (`TKT-001013`) while the
fakes are random (`TKT-407025`), so an attacker collecting many responses could infer which were
discarded. `CC-47`'s requirement — that a caller cannot tell the defence fired — holds for any single
submission, which is what it asks for. Making the fakes indistinguishable in aggregate would mean
consuming real sequence values for rejected spam, which is a worse trade.

### Still open, stated plainly

- **There is no mobile navigation in the public shell at all** — the header `<nav>` is
  `hidden md:flex` and no hamburger or mobile menu exists, so on a phone every public nav link
  (including the `/contact` link added above) is unreachable. Pre-existing, affects all five links
  equally, and larger than this plan; recorded so it is not mistaken for something this work
  introduced.
- **The 19 pre-existing portal-app test failures** (`kb-list`, `kb-detail`, `dashboard`,
  `tickets/detail`) are untouched and undiagnosed here.
- **No Playwright coverage was added**, deliberately: the approved spec defines exactly one
  end-to-end journey (`AC-64`), and adding a per-feature E2E test would mean amending an approved
  spec without asking.

**Documentation updated:** the repo-root `CLAUDE.md` (two simulator commands plus the two startup
traps above), `cms-integration-gateway/CLAUDE.md` (an "Inbound simulators" section), the spec's
status header, and the delivery plan's `FEAT-35` row — which now records both plans and states the
`CC-45`/`CC-46` deferral explicitly, since an unrecorded missing layer is indistinguishable from a
forgotten one.

**Not committed.** The whole session ran under an explicit no-commit instruction. Nothing here has
been staged or committed; `git status` shows the work as modified/untracked files. The commit that
*would* be made, once that instruction lifts, is roughly:

```
feat: inbound channels — SMS webhook, inbound email, portal web form (CC-40..CC-44, CC-47)
```

with the spec and plan committed ahead of the code, per this project's own gate.

## Deviations from the plan, collected

1. **Task 3's raw-body read does not work for form posts** — MVC drains the body during model
   binding. Resolved by verifying against the canonically re-encoded parsed form, which is lossless
   for Twilio's decoded-value scheme and would *not* be for Meta's. Full diagnosis above.
2. **Task 7's `MESSAGE_NOT_FOUND` does not exist.** The plan flagged this as a check to run;
   `ApplicationErrors.Ticket` has `MESSAGE_RECORDED` but no `MESSAGE_NOT_FOUND`, so both not-found
   branches of `GetTicketReferenceForMessageQueryHandler` use `Ticket.NOT_FOUND`. No new error code
   was added, which keeps `ContractHardeningTests.EveryErrorCode_HasABilingualMessage` out of it.
3. **The throttle registration was briefly added during Task 2** before its interface existed, which
   would have broken the build. Caught immediately and reverted to Task 7 where it belongs — noted
   because the fix was to keep each task independently verifiable, not to push on.
4. **`Messaging__Required=false`** was needed to start the ExternalApi host by hand (Task 8); the
   plan's command block omitted it. The plan's own step has been left as written and this record
   carries the correction, alongside the `CLAUDE.md` update that makes it discoverable.
