# FEAT-35 — Channel mock gateway and the mock/real toggle (plan 1 of 2: outbound)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every outbound channel actually send — first by fixing the credential defect that
stops any request leaving the process — then let one configuration flag point Email, WhatsApp and
SMS at provider-faithful mocks in `cms-integration-gateway` or at real providers.

**Architecture:** The toggle is a decorator over `IExternalApiConfigurationProvider`, the single port
every sender reads its base URL and credential through, so no sender or handler learns about mocks.
The three near-identical HTTP senders collapse onto one base that owns transport, retry and auth,
leaving each channel a small adapter owning only its provider's payload shape and message-id
extraction. On the Node side, one model per provider under provider-shaped routes, plus the one
extension `gateway-handler.js` needs to be able to answer with a real status code and headers.

**Tech Stack:** .NET 10 / C# (xUnit + FluentAssertions + Moq), Node 20 / Express + json-server.

**Spec:** [`EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md`](../../specs/EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md)
— read the **Amendment — 2026-09-02** section; this plan implements `CC-30`–`CC-39`, `CC-48`,
`CC-49`, `CC-51`.

**Plan 2 of 2 (not this document)** covers inbound: `CC-40`–`CC-47`, `CC-50`.

## Global Constraints

- The dependency rule does not bend: `Domain` references nothing, `Application` never references
  `Infrastructure`. `ChannelOptions` therefore lives in `Application`, its consumers in `Infrastructure`.
- `Channels:UseMocks` defaults to **`false`**. A missing configuration section must behave exactly
  as today (`CC-30`).
- `Channels:UseMocks == true` under `Production` must fail startup (`CC-32`).
- No test may depend on the Node gateway running on port 3001 (`CC-50`).
- Never log a credential, a token, or a full provider payload (`CC-29`).
- `TicketMessage.Channel` is `nvarchar(20)`; every channel name must fit.
- Existing green tests stay green. The one red test at the start —
  `WhatsAppOutboundReplyTests.CC10_WhatsAppReply_RecordsOutboundMessageAndDispatchesToTheGateway`
  — must be green from Task 1 onward and never regress.
- Build with `cd backend && dotnet build CustomerSupport.slnx`; test with
  `dotnet test CustomerSupport.slnx --filter "<expr>"`.

---

## File structure

**Created**

| File | Responsibility |
|---|---|
| `backend/src/CustomerSupport.Application/Common/Options/ChannelOptions.cs` | The `Channels:*` settings: `UseMocks`, `MockBaseUrl`, `MockWebhookSecret`, `EmailFrom`, `SmsFrom`. Application layer, so both hosts and the decorator can bind it. |
| `backend/src/CustomerSupport.Application/Channels/ChannelMockGuard.cs` | One pure function deciding whether a `(useMocks, environmentName)` pair is legal. Pure so `CC-32` is a unit test, not a host boot. |
| `backend/src/CustomerSupport.Domain/Common/ChannelNames.cs` | The single source of truth for permitted channel names (`CC-48`). |
| `backend/src/CustomerSupport.Infrastructure/ExternalApis/Providers/MockRoutingExternalApiConfigurationProvider.cs` | The decorator. Answers the three gateway names with mock routes when the flag is on; delegates everything else (`CC-30`, `CC-31`, `CC-33`). |
| `backend/src/CustomerSupport.Infrastructure/Notifications/Channels/ChannelHttpSender.cs` | Abstract base owning config lookup, client construction, auth, the retry loop and result mapping (`CC-49`). |
| `cms-integration-gateway/models/SendGridGatewayModel.js` | SendGrid v3 mock (`CC-34`). |
| `cms-integration-gateway/models/MetaWhatsAppGatewayModel.js` | Meta Cloud API v18 mock (`CC-35`). |
| `cms-integration-gateway/models/TwilioGatewayModel.js` | Twilio Messages mock (`CC-36`). |
| `cms-integration-gateway/behaviors/provider-failure-rules.js` | Deterministic permanent/transient triggers shared by all three mocks (`CC-37`, `CC-38`). |
| `cms-integration-gateway/mocks/providers/history.json` | Sent-message log the three mocks append to, visible in Mock Manager. |

**Modified**

| File | Change |
|---|---|
| `backend/src/CustomerSupport.Infrastructure/Notifications/EmailNotificationChannelSender.cs` | Becomes a SendGrid adapter on the base. |
| `.../SmsNotificationChannelSender.cs` | Becomes a Twilio adapter on the base. |
| `.../WhatsAppNotificationChannelSender.cs` | Becomes a Meta adapter on the base; stops fabricating its message id. |
| `.../ServiceCollectionExtensions.cs:68` | Registers the decorator around `DatabaseExternalApiProvider`; binds and validates `ChannelOptions`. |
| `backend/src/CustomerSupport.Domain/Entities/Tickets/TicketMessage.cs:17` | Reads its allow-list from `ChannelNames`. |
| `.../RecordTicketMessageCommandValidator.cs:9`, `.../IngestInboundChannelMessageCommandValidator.cs:8`, `.../CreateTicketCommandValidator.cs:54` | Same. |
| `backend/tests/.../Unit/Notifications/WhatsAppNotificationChannelSenderTests.cs` | `RecordingHttpMessageHandler` gains queued bodies/headers; `CreateSut` drops the protector. |
| `cms-integration-gateway/middlewares/gateway-handler.js:50` | Lets a model answer with a status code, headers and an empty body. |
| `cms-integration-gateway/models/ServiceRegistry.js` | Registers the three new models. |
| `cms-integration-gateway/.env.example`, `config.js` | `CALLBACK_BASE_URL`, `WEBHOOK_SECRET`. |
| `cms-integration-gateway/CLAUDE.md` | "Current Services" gains the three provider mocks. |
| `CLAUDE.md` (repo root) | Commands table gains the gateway and the flag. |

---

## Task 1: Make outbound sending work at all (`CC-51`, closes `CC-10`/`CC-13`)

`DatabaseExternalApiProvider.MapToConfig` (`ExternalApis/Providers/DatabaseExternalApiProvider.cs:96-101`)
already decrypts every credential, so `GetConfig` returns plaintext. Each sender's `ApplyAuth` then
calls `Unprotect` on that plaintext, `IDataProtector.Unprotect` throws, and because `ApplyAuth` runs
*outside* the retry `try`, the exception escapes `SendAsync` and is swallowed by
`NotificationGateway.cs:93-99` as `DELIVERY_FAILED`. The provider owns decryption; senders receive
plaintext. Fix: delete the second unprotect.

**Files:**
- Modify: `backend/src/CustomerSupport.Infrastructure/Notifications/EmailNotificationChannelSender.cs:85-106`
- Modify: `backend/src/CustomerSupport.Infrastructure/Notifications/SmsNotificationChannelSender.cs:85-106`
- Modify: `backend/src/CustomerSupport.Infrastructure/Notifications/WhatsAppNotificationChannelSender.cs:93-114`
- Test: `backend/tests/CustomerSupport.Tests/Unit/Notifications/WhatsAppNotificationChannelSenderTests.cs`
- Modify (found during execution, not originally planned):
  `backend/tests/CustomerSupport.Tests/Integration/GatewayTestData.cs` — seed both `authValue` and
  `authToken`, same protected secret.
- Modify (found during execution, not originally planned):
  `backend/tests/CustomerSupport.Tests/Integration/WhatsAppOutboundReplyTests.cs:29` — compose the
  `/messages` path onto the stub's URL at the one call site that dereferences it.

**Interfaces:**
- Consumes: nothing.
- Produces: all three senders' constructors lose their `ISecretProtector` parameter. Task 4 depends
  on that shape.

- [x] **Step 1: Write the failing test**

Add to `WhatsAppNotificationChannelSenderTests.cs`. It asserts the plaintext credential the provider
hands over reaches the header — which today throws instead.

```csharp
[Fact]
public async Task CC51_PlaintextCredentialFromTheProvider_ReachesTheAuthorizationHeader()
{
    // The provider decrypts before handing the config over (DatabaseExternalApiProvider.MapToConfig),
    // so a sender must treat Auth.Token as plaintext and never unprotect it a second time.
    _configProvider
        .Setup(p => p.GetConfig(NotificationGatewayConstants.WhatsAppGatewayConfigName))
        .Returns(new ExternalApiConfig
        {
            BaseUrl = ApiBaseUrl,
            TimeoutSeconds = 30,
            Auth = new ExternalApiAuthConfig
            {
                Type = ExternalApiAuthType.Bearer,
                Token = "already-decrypted-token",
            },
        });
    _recorder.Queue(HttpStatusCode.OK);

    var result = await CreateSut().SendAsync(Notification());

    result.Succeeded.Should().BeTrue();
    _recorder.Calls.Should().HaveCount(1);
    _recorder.Calls[0].Authorization.Should().Be("Bearer already-decrypted-token");
}
```

- [x] **Step 2: Run it and watch it fail**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~CC51_PlaintextCredential"
```

Expected: FAIL. With `IdentitySecretProtector` still injected it passes vacuously, so **first delete
`new IdentitySecretProtector(),` from `CreateSut()`** (step 3 removes the parameter). Before the fix,
compilation fails on the missing argument — that is the failing state for this step.

- [x] **Step 3: Remove the double-unprotect from all three senders**

In each of the three files: drop the `ISecretProtector` field, constructor parameter and assignment,
drop the `using CustomerSupport.Application.Interfaces;` only if nothing else needs it, and change
`ApplyAuth` to use the values as given:

```csharp
    private static void ApplyAuth(HttpClient client, ExternalApiAuthConfig auth)
    {
        // The configuration provider has already decrypted these (DatabaseExternalApiProvider
        // .MapToConfig). Unprotecting again throws, which is what silently broke every outbound
        // send — see spec A19 / CC-51.
        switch (auth.Type)
        {
            case ExternalApiAuthType.ApiKey:
                client.DefaultRequestHeaders.Remove(auth.KeyName);
                client.DefaultRequestHeaders.Add(auth.KeyName, auth.Value);
                break;
            case ExternalApiAuthType.Bearer:
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", auth.Token);
                break;
            case ExternalApiAuthType.Basic:
                var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{auth.ClientId}:{auth.ClientSecret}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
                break;
            case ExternalApiAuthType.OAuth2:
            case ExternalApiAuthType.None:
            default:
                break;
        }
    }
```

`GatewayTestData.cs:37` seeds `authType: "Bearer"` but sets `authValue`, leaving `AuthToken` null —
so the Bearer branch would send an empty token. Change that seed to
`authToken: protector.Protect(WhatsAppAppSecret)` if `ExternalApiConfiguration.Create` exposes it;
if it does not, seed `authType: "ApiKey"` with `authKeyName: "Authorization"`. Read
`Domain/Entities/ExternalApis/ExternalApiConfiguration.cs` and pick whichever the factory supports —
do not add a parameter to the entity for this.

> **This guidance was incomplete — see the deviation record below.** Switching the seed to
> `authToken` alone (dropping `authValue`) fixes the outbound Bearer header but breaks
> `MetaSignatureVerifier`, which reads `Auth.Value`, not `Auth.Token`, to verify an inbound webhook's
> signature. The two fields serve genuinely different consumers even though this test fixture uses
> one secret constant for both. **Seed both `authValue` and `authToken` with the same protected
> secret.**

Guard against an empty credential rather than sending a malformed header:

```csharp
            case ExternalApiAuthType.Bearer when !string.IsNullOrWhiteSpace(auth.Token):
```

- [x] **Step 4: Run the unit test and the red integration test**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~WhatsAppNotificationChannelSenderTests|FullyQualifiedName~WhatsAppOutboundReply"
```

Expected: PASS, 9/9 — including
`CC10_WhatsAppReply_RecordsOutboundMessageAndDispatchesToTheGateway`, which was failing with
`Expected _stub.ReceivedBodies to contain 1 item(s), but found 0`.

> **Did not pass 9/9 on the first attempt after step 3 alone.** `CC10_WhatsAppReply...` was *still*
> red — same assertion, same "found 0" — after removing the double-unprotect. Root-caused by
> instrumenting the test host with a temporary diagnostic logger and a `Console.WriteLine` in
> `RecordTicketMessageCommandHandler` (both removed before commit): the dispatch was actually being
> attempted and `NotificationGateway` reported `DELIVERY_FAILED` with no exception logged, which
> meant the HTTP call completed but failed non-transiently — a 404. `GatewayTestData.
> SeedWhatsAppGatewayAsync(_stub.BaseUrl)` was seeding the stub's **bare host**, but
> `StubGatewayServer` maps its handler at `/messages`; every outbound POST hit the stub's root and
> 404'd at ASP.NET's routing layer before any endpoint ran — non-transient, one attempt, zero bodies
> received. A second, independent bug from `CC-51`, in test code rather than production code. Fixed
> by moving the `/messages` composition into `WhatsAppOutboundReplyTests.InitializeAsync` (the one
> caller that actually dereferences the URL) rather than into `GatewayTestData` itself, since
> `WhatsAppWebhookTests` passes its own complete fake URL to the same helper and never dereferences
> it — appending a path there would have doubled it.
>
> Fixing that in turn **broke `WhatsAppWebhookTests.CC8_SignedWebhook...` and `CC9_RetriedDelivery...`**,
> caught only by running the full suite and diffing named failures against a baseline at `HEAD` (see
> below) — this is the `Auth.Value`/`Auth.Token` split described in the step above. All 15 WhatsApp
> unit + integration tests (8 sender unit + 2 outbound reply + 5 webhook) pass together after both
> fixes.

- [x] **Step 5: Run the whole suite — this touched a shared path**

```bash
cd backend && dotnet test CustomerSupport.slnx
```

Paste the counts into the task record. Any newly-red test is a real regression; do not proceed.

> **Actual verification used a name-level diff against a baseline, not just a count comparison** —
> a raw "Failed: N" count cannot distinguish "fixed one, broke another" from "no change". Reverted
> this task's 6 files to `HEAD` (backed up first), ran the full suite (`Failed: 57, Passed: 730,
> Total: 787`), restored the files, ran again with everything fixed (`Failed: 56, Passed: 732, Total:
> 788`), and diffed the two `[FAIL]`-line name sets directly (`dotnet test`'s own console failure
> count corresponds to the TRX `RunInfo` elements with `outcome="Error"`, not the `UnitTestResult`
> `outcome="Failed"` count, which includes extra entries a raw grep does not explain — the `RunInfo`
> text lines were used as the reliable source). Result: exactly one name moved from failing to
> passing (`WhatsAppOutboundReplyTests.CC10_WhatsAppReply_RecordsOutboundMessageAndDispatchesToTheGateway`);
> the set of 56 remaining failures is byte-for-byte identical between the two runs. Zero regressions,
> confirmed by name, not by count.
>
> The 56 pre-existing failures span unrelated areas (`PermissionTests`, `AuditLogEndpointTests`,
> `TicketLifecycleEndpointTests`, and even pure `Domain`/`Validators` unit tests with no HTTP or DB
> dependency) and are consistent with the sandbox permission/identity-seeding defect this project's
> own `FEAT-34` plan record already documents as pre-existing and unrelated to any single feature.
> Not investigated further here — out of this task's scope, and already on record elsewhere.

- [ ] **Step 6: Commit** — **deliberately not executed.** Explicit instruction this session:
  implement the work, do not commit it. `git status` shows exactly the 6 files below, uncommitted,
  ready for the next commit:
  `EmailNotificationChannelSender.cs`, `SmsNotificationChannelSender.cs`,
  `WhatsAppNotificationChannelSender.cs`, `GatewayTestData.cs`, `WhatsAppOutboundReplyTests.cs`,
  `WhatsAppNotificationChannelSenderTests.cs`. The commit message drafted in this plan still applies;
  it should additionally mention the `Auth.Value`/`Auth.Token` split and the stub-URL path fix, since
  both shipped in the same change.

---

## Task 2: One channel allow-list (`CC-48`)

Four lists disagree today: the entity permits seven values, `RecordTicketMessage`'s validator six
(missing `Portal`), the ingest validator three, and `CreateTicket`'s `Source` set a sixth. Adding a
channel currently means finding all four.

**Files:**
- Create: `backend/src/CustomerSupport.Domain/Common/ChannelNames.cs`
- Modify: `backend/src/CustomerSupport.Domain/Entities/Tickets/TicketMessage.cs:17`
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/RecordTicketMessage/RecordTicketMessageCommandValidator.cs:9`
- Modify: `backend/src/CustomerSupport.Application/Features/Channels/Commands/IngestInboundChannelMessage/IngestInboundChannelMessageCommandValidator.cs:8`
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandValidator.cs:54-55`
- Test: `backend/tests/CustomerSupport.Tests/Unit/Domain/ChannelNamesTests.cs`

**Interfaces:**
- Produces: `ChannelNames.All`, `ChannelNames.Inbound`, `ChannelNames.TicketSources`, and the
  constants `Email`, `Sms`, `WhatsApp`, `WebForm`, `LiveChat`, `Portal`, `System`. Task 4 and plan 2
  both use these.

- [ ] **Step 1: Write the failing test**

```csharp
using CustomerSupport.Domain.Common;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

public class ChannelNamesTests
{
    [Fact]
    public void CC48_AllContainsEverySupportedChannel()
    {
        ChannelNames.All.Should().BeEquivalentTo(
            ["Email", "System", "WhatsApp", "SMS", "WebForm", "LiveChat", "Portal"]);
    }

    [Fact]
    public void CC48_InboundIsASubsetOfAll_AndIncludesEmail()
    {
        ChannelNames.Inbound.Should().BeSubsetOf(ChannelNames.All);
        ChannelNames.Inbound.Should().Contain("Email");
    }

    [Fact]
    public void CC48_EveryNameFitsThePersistedColumn()
    {
        // TicketMessageConfiguration.cs:15 caps Channel at 20 characters.
        ChannelNames.All.Should().OnlyContain(name => name.Length <= 20);
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ChannelNamesTests"
```

Expected: FAIL — `ChannelNames` does not exist.

- [x] **Step 3: Create the single source of truth**

```csharp
namespace CustomerSupport.Domain.Common;

/// <summary>
/// The permitted channel names, in one place (CC-48). Four divergent copies existed before this:
/// the entity's own array, two command validators, and CreateTicket's Ticket.Source set — and they
/// disagreed, with `Portal` missing from one and `Email` absent from the inbound list entirely.
/// Every name must fit TicketMessage.Channel's nvarchar(20).
/// </summary>
public static class ChannelNames
{
    public const string Email = "Email";
    public const string System = "System";
    public const string WhatsApp = "WhatsApp";
    public const string Sms = "SMS";
    public const string WebForm = "WebForm";
    public const string LiveChat = "LiveChat";
    public const string Portal = "Portal";

    /// <summary>Every value TicketMessage.Channel may hold.</summary>
    public static readonly string[] All = [Email, System, WhatsApp, Sms, WebForm, LiveChat, Portal];

    /// <summary>
    /// Channels an inbound customer message can arrive on. `System` is machine-authored and
    /// `Portal` has its own authenticated command, so neither is ingestible here.
    /// </summary>
    public static readonly string[] Inbound = [Email, WhatsApp, Sms, WebForm, LiveChat];

    /// <summary>Values Ticket.Source may hold — where a ticket originated.</summary>
    public static readonly string[] TicketSources = [Portal, WebForm, WhatsApp, Sms, Email, LiveChat];

    public static bool IsKnown(string? channel) =>
        channel is not null && Array.Exists(All, c => string.Equals(c, channel, StringComparison.Ordinal));
}
```

- [x] **Step 4: Point all four call sites at it**

`TicketMessage.cs:17`:

```csharp
    private static readonly string[] AllowedChannels = ChannelNames.All;
```

`RecordTicketMessageCommandValidator.cs:9`:

```csharp
    private static readonly string[] AllowedChannels = ChannelNames.All;
```

`IngestInboundChannelMessageCommandValidator.cs:8`:

```csharp
    private static readonly string[] AllowedChannels = ChannelNames.Inbound;
```

`CreateTicketCommandValidator.cs:54-55` — replace the inline set with `ChannelNames.TicketSources`,
keeping the existing `Contains` check and error code exactly as they are.

Add `using CustomerSupport.Domain.Common;` where needed. Delete the stale comment listing the
allow-list at `Application/Channels/Contracts.cs:9`.

- [x] **Step 5: Run the tests**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ChannelNamesTests|FullyQualifiedName~TicketMessageTests|FullyQualifiedName~IngestInboundChannelMessage|FullyQualifiedName~CreateTicketCommandValidator"
```

Expected: PASS. `TicketMessageTests.Create_AnyAllowedChannel_IsAccepted` now iterates the shared
list. Widening the inbound list to include `Email` and `LiveChat` must not break
`CC4_UnrecognisedChannel_IsRejectedBeforeAnyWrite` — if that test used `"Email"` as its example of
an unrecognised channel, change the example to `"Telegram"` and say so in the task record.

> **Confirmed `CC4_UnrecognisedChannel_IsRejectedBeforeAnyWrite` uses `"Carrier Pigeon"`, not
> `"Email"`** — no collision, no change needed. One failure appeared when this filter was run
> broader than planned (`+FullyQualifiedName~RecordTicketMessage`, 37 tests):
> `IngestInboundChannelMessageTests.CC2_MessageAfterResolution_StartsANewTicket`, error `"Ticket
> 'TKT-001004' cannot be resolved without a resolution code and notes."` — a FEAT-32 resolution-
> discipline requirement, unrelated to channel names. Confirmed pre-existing by grepping it against
> both `/tmp/baseline-failed-names.txt` and `/tmp/final-failed-names.txt` from Task 1's verification
> — present in both. The narrower, originally-planned filter (`ChannelNamesTests|TicketMessageTests|
> CreateTicketCommandValidator|RecordTicketMessage`, excluding `IngestInboundChannelMessage`) runs
> **29/29 green**.
>
> Per instruction this session, the full-suite name-diff performed for Task 1 was **not** repeated
> for this task — it is a mechanical extract-constant refactor (every list's values are unchanged,
> only their storage location moves), a materially lower-risk shape than Task 1's behavioural fix,
> and Task 1 already established the 56-name pre-existing baseline this task's one incidental
> failure was checked against.

- [ ] **Step 6: Commit** — **not executed.** Same instruction as Task 1: implement and verify,
  commit nothing. `git status` after this task shows two new files
  (`ChannelNames.cs`, `ChannelNamesTests.cs`) and five modified
  (`TicketMessage.cs`, `RecordTicketMessageCommandValidator.cs`,
  `IngestInboundChannelMessageCommandValidator.cs`, `CreateTicketCommandValidator.cs`,
  `Application/Channels/Contracts.cs`), on top of Task 1's six.

---

## Task 3: The mock/real toggle (`CC-30`–`CC-33`)

**Files:**
- Create: `backend/src/CustomerSupport.Application/Common/Options/ChannelOptions.cs`
- Create: `backend/src/CustomerSupport.Application/Channels/ChannelMockGuard.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/ExternalApis/Providers/MockRoutingExternalApiConfigurationProvider.cs`
- Modify: `backend/src/CustomerSupport.Infrastructure/ServiceCollectionExtensions.cs:68`
- Modify: `backend/src/CustomerSupport.InternalApi/appsettings.json`, `backend/src/CustomerSupport.ExternalApi/appsettings.json`
- Test: `backend/tests/CustomerSupport.Tests/Unit/Channels/MockRoutingExternalApiConfigurationProviderTests.cs`

**Interfaces:**
- Consumes: `NotificationGatewayConstants.{Email,Sms,WhatsApp}GatewayConfigName`.
- Produces: `ChannelOptions` (`SectionName = "Channels"`; `UseMocks`, `MockBaseUrl`,
  `MockWebhookSecret`, `EmailFrom`, `SmsFrom`); `ChannelMockGuard.Validate(bool, string?)`. Tasks
  6 and 7 read `EmailFrom`/`SmsFrom`.

- [x] **Step 1: Write the failing tests**

```csharp
using CustomerSupport.Application.Channels;
using CustomerSupport.Application.Common.Options;
using CustomerSupport.Application.ExternalApis.DTOs;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Infrastructure.ExternalApis.Providers;
using FluentAssertions;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Channels;

public class MockRoutingExternalApiConfigurationProviderTests
{
    private readonly Mock<IExternalApiConfigurationProvider> _inner = new();

    private static ChannelOptions Options(bool useMocks) => new()
    {
        UseMocks = useMocks,
        MockBaseUrl = "http://localhost:3001",
        MockWebhookSecret = "dev-secret",
    };

    [Fact]
    public void CC30_FlagOff_DelegatesEverythingToTheDatabaseProvider()
    {
        var fromDb = new ExternalApiConfig { BaseUrl = "https://real.example/send" };
        _inner.Setup(p => p.GetConfig(NotificationGatewayConstants.EmailGatewayConfigName)).Returns(fromDb);

        var sut = new MockRoutingExternalApiConfigurationProvider(_inner.Object, Options(useMocks: false));

        sut.GetConfig(NotificationGatewayConstants.EmailGatewayConfigName).Should().BeSameAs(fromDb);
    }

    [Theory]
    [InlineData("EmailGateway", "http://localhost:3001/mock/sendgrid/v3/mail/send")]
    [InlineData("WhatsAppGateway", "http://localhost:3001/mock/meta/v18.0/100000000000000/messages")]
    public void CC31_FlagOn_RoutesTheChannelGatewaysToTheMock(string configName, string expectedUrl)
    {
        var sut = new MockRoutingExternalApiConfigurationProvider(_inner.Object, Options(useMocks: true));

        var config = sut.GetConfig(configName);

        config.Should().NotBeNull();
        config!.BaseUrl.Should().Be(expectedUrl);
        _inner.Verify(p => p.GetConfig(configName), Times.Never);
    }

    [Fact]
    public void CC31_FlagOn_LeavesEveryOtherConfigurationAlone()
    {
        var payments = new ExternalApiConfig { BaseUrl = "https://payments.example" };
        _inner.Setup(p => p.GetConfig("PaymentGateway")).Returns(payments);

        var sut = new MockRoutingExternalApiConfigurationProvider(_inner.Object, Options(useMocks: true));

        sut.GetConfig("PaymentGateway").Should().BeSameAs(payments);
    }

    [Fact]
    public void CC33_FlagOn_WorksWithNoDatabaseRowAtAll()
    {
        _inner.Setup(p => p.GetConfig(It.IsAny<string>())).Returns((ExternalApiConfig?)null);

        var sut = new MockRoutingExternalApiConfigurationProvider(_inner.Object, Options(useMocks: true));

        var config = sut.GetConfig(NotificationGatewayConstants.SmsGatewayConfigName);

        config.Should().NotBeNull();
        // Auth.Type None keeps ApplyAuth out of the way; the secret is still carried for the
        // inbound verifier, which reads Auth.Value regardless of Type.
        config!.Auth.Type.Should().Be(ExternalApiAuthType.None);
        config.Auth.Value.Should().Be("dev-secret");
    }

    [Theory]
    [InlineData(true, "Production", false)]
    [InlineData(true, "Development", true)]
    [InlineData(false, "Production", true)]
    [InlineData(true, null, true)]
    public void CC32_MocksAreIllegalInProductionOnly(bool useMocks, string? environment, bool expectedLegal)
    {
        ChannelMockGuard.Validate(useMocks, environment).IsLegal.Should().Be(expectedLegal);
    }
}
```

- [x] **Step 2: Run and watch it fail**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~MockRoutingExternalApiConfigurationProviderTests"
```

Expected: FAIL — none of the three new types exist.

- [x] **Step 3: Add the options and the guard (Application)**

```csharp
namespace CustomerSupport.Application.Common.Options;

/// <summary>
/// The `Channels:*` settings. `UseMocks` swaps the three channel gateways over to
/// cms-integration-gateway's provider-faithful mocks (CC-30/CC-31); everything else stays on the
/// database configuration. Lives in Application so both hosts and the Infrastructure decorator can
/// bind it without Application referencing Infrastructure.
/// </summary>
public sealed class ChannelOptions
{
    public const string SectionName = "Channels";

    public bool UseMocks { get; set; }

    public string MockBaseUrl { get; set; } = "http://localhost:3001";

    /// <summary>Shared with the mock so its outbound webhooks carry a signature we can verify.</summary>
    public string MockWebhookSecret { get; set; } = string.Empty;

    /// <summary>SendGrid requires a `from`; the old house payload had none.</summary>
    public string EmailFrom { get; set; } = "no-reply@commandcenter.local";

    /// <summary>Twilio requires a `From`.</summary>
    public string SmsFrom { get; set; } = "CommandCenter";
}
```

```csharp
namespace CustomerSupport.Application.Channels;

/// <summary>
/// CC-32 — mocks must never be active in production. A mock gateway that accepts and discards
/// customer notifications is worse than an outage, because every send reports success and nothing
/// alerts. A pure function so the rule is unit-tested without booting a host.
/// </summary>
public static class ChannelMockGuard
{
    public static (bool IsLegal, string? Error) Validate(bool useMocks, string? environmentName)
    {
        var isProduction = string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);

        return useMocks && isProduction
            ? (false, "Channels:UseMocks must not be true when the environment is Production. "
                    + "Remove the setting or point the channel gateways at real providers.")
            : (true, null);
    }
}
```

- [x] **Step 4: Add the decorator (Infrastructure)**

```csharp
using CustomerSupport.Application.Common.Options;
using CustomerSupport.Application.ExternalApis.DTOs;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;

namespace CustomerSupport.Infrastructure.ExternalApis.Providers;

/// <summary>
/// CC-30/CC-31/CC-33 — the whole mock/real toggle. Every channel sender and the inbound signature
/// verifier read their base URL and credential through IExternalApiConfigurationProvider and
/// nothing else, so decorating that one port is enough: no sender, handler or controller learns
/// that mocks exist.
/// </summary>
public sealed class MockRoutingExternalApiConfigurationProvider : IExternalApiConfigurationProvider
{
    /// <summary>
    /// Provider-faithful paths, so flipping the flag changes only the host. The account sid and
    /// phone-number id are fixed dev values the mock also hard-codes; in real mode both arrive
    /// inside the configured BaseUrl instead.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> MockPaths =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [NotificationGatewayConstants.EmailGatewayConfigName] = "mock/sendgrid/v3/mail/send",
            [NotificationGatewayConstants.SmsGatewayConfigName] = "mock/twilio/2010-04-01/Accounts/ACmockaccountsid/Messages.json",
            [NotificationGatewayConstants.WhatsAppGatewayConfigName] = "mock/meta/v18.0/100000000000000/messages",
        };

    private readonly IExternalApiConfigurationProvider _inner;
    private readonly ChannelOptions _options;

    public MockRoutingExternalApiConfigurationProvider(
        IExternalApiConfigurationProvider inner,
        ChannelOptions options)
    {
        _inner = inner;
        _options = options;
    }

    public ExternalApiConfig? GetConfig(string apiName) =>
        _options.UseMocks && MockPaths.TryGetValue(apiName, out var path)
            ? MockConfig(path)
            : _inner.GetConfig(apiName);

    public IReadOnlyList<ExternalApiConfig> GetAllConfigs() => _inner.GetAllConfigs();

    public Task ReloadAsync(CancellationToken ct = default) => _inner.ReloadAsync(ct);

    private ExternalApiConfig MockConfig(string path) => new()
    {
        BaseUrl = $"{_options.MockBaseUrl.TrimEnd('/')}/{path}",
        TimeoutSeconds = 30,
        Auth = new ExternalApiAuthConfig
        {
            // None: the mock needs no credential, and this keeps ApplyAuth from building a header
            // out of a value nobody set. Value still carries the shared secret because the inbound
            // signature verifier reads Auth.Value irrespective of Type.
            Type = ExternalApiAuthType.None,
            Value = _options.MockWebhookSecret,
        },
    };
}
```

- [x] **Step 5: Register it and enforce the guard**

Replace `ServiceCollectionExtensions.cs:68` with:

```csharp
        services.Configure<ChannelOptions>(configuration.GetSection(ChannelOptions.SectionName));

        var channelOptions = configuration.GetSection(ChannelOptions.SectionName).Get<ChannelOptions>()
            ?? new ChannelOptions();
        var guard = ChannelMockGuard.Validate(
            channelOptions.UseMocks,
            configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"]);
        if (!guard.IsLegal)
        {
            throw new InvalidOperationException(guard.Error);
        }

        services.AddSingleton<DatabaseExternalApiProvider>();
        services.AddSingleton<IExternalApiConfigurationProvider>(sp =>
        {
            var inner = sp.GetRequiredService<DatabaseExternalApiProvider>();
            return channelOptions.UseMocks
                ? new MockRoutingExternalApiConfigurationProvider(inner, channelOptions)
                : inner;
        });
```

Add `using CustomerSupport.Application.Channels;` and
`using CustomerSupport.Application.Common.Options;`. `ExternalApiConfigurationsController` calls
`ReloadAsync` through the interface — the decorator delegates it, so admin reload keeps working.

Add to **both** `appsettings.json` files, above `"ExternalApis"`:

```json
  "Channels": {
    "UseMocks": false,
    "MockBaseUrl": "http://localhost:3001",
    "MockWebhookSecret": "",
    "EmailFrom": "no-reply@commandcenter.local",
    "SmsFrom": "CommandCenter"
  },
```

- [x] **Step 6: Run the tests**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~MockRoutingExternalApiConfigurationProviderTests"
cd backend && dotnet test CustomerSupport.slnx
```

Expected: the new tests PASS and the suite stays at Task 1's counts — `UseMocks` defaults to false,
so nothing else changes behaviour (`CC-30`).

> **The full-suite run was not repeated** (per instruction this session — Task 1 already established
> the 56-name baseline; re-running the whole ~9-minute suite for every task is disproportionate to
> risk once that baseline exists). Instead: 9/9 new tests pass, and a wider targeted run covering
> everything Tasks 1–3 touch — `MockRoutingExternalApiConfigurationProviderTests`,
> `ChannelNamesTests`, `WhatsAppNotificationChannelSenderTests`, `WhatsAppOutboundReplyTests`,
> `WhatsAppWebhookTests`, `TicketMessageTests`, `CreateTicketCommandValidator*`,
> `RecordTicketMessage*` — is **53/53 green**.
>
> **One real incident during this step, resolved without any code change.** Running a broader ad
> hoc filter that included `TicketMessagesEndpointTests` showed 9 failures
> (`System.InvalidOperationException: Sequence contains no elements` in a test's own
> `CreateTicketAsync()` helper, fetching `/api/Categories` and finding it empty). This looked like it
> could be this task's DI registration change breaking category seeding — decisive isolation proved
> otherwise: the identical 9/13 failure reproduces with `ServiceCollectionExtensions.cs` fully
> reverted to `HEAD`, and again with **every** file from Tasks 1 and 3 reverted to `HEAD`
> simultaneously (Task 2's `CreateTicketCommandValidator.cs`/`ChannelNames.cs` had to stay, since
> `CreateTicketCommand.cs` — untouched by any of these tasks — already dropped the `Priority`
> property in FEAT-32's own uncommitted work, and pure `HEAD`'s validator referencing it no longer
> compiles against that). `TicketMessagesEndpointTests.cs` itself is one of the ~50
> pre-existing-uncommitted FEAT-32 files in this working tree. Recorded here, not chased further —
> out of scope for this feature, and not this task's regression by direct, repeated proof.

- [ ] **Step 7: Commit** — **not executed**, same instruction as Tasks 1–2.

---

## Task 4: Collapse the three senders onto one base (`CC-49`)

`ApplyAuth` and both `IsTransient` overloads are verbatim copies in three files, and the retry loop
is duplicated with only the payload and the result channel differing. Two latent bugs go with the
duplication: the id is **fabricated** (`$"wa:{Guid.NewGuid():N}"`) rather than read from the
provider, and a single `StringContent` is reused across retry attempts even though its stream is
consumed by the first `PostAsync`.

**Files:**
- Create: `backend/src/CustomerSupport.Infrastructure/Notifications/Channels/ChannelHttpSender.cs`
- Modify: all three senders
- Modify: `backend/tests/CustomerSupport.Tests/Unit/Notifications/WhatsAppNotificationChannelSenderTests.cs` (recorder gains bodies and headers)

**Interfaces:**
- Consumes: Task 1's constructor shape (no `ISecretProtector`).
- Produces: `ChannelHttpSender` with abstract members `SupportedChannel`, `ConfigName`,
  `BuildContent(RenderedNotification)`, `ReadProviderMessageIdAsync(HttpResponseMessage, CancellationToken)`,
  and protected helper `JsonContent(object)`. Tasks 5–7 implement against exactly these.
  `RecordingHttpMessageHandler.Queue(HttpStatusCode, string? body = null, IEnumerable<(string, string)>? headers = null)`.

- [x] **Step 1: Extend the recorder so a test can queue a real provider response**

In `WhatsAppNotificationChannelSenderTests.cs`, replace the queue field and `Queue`/`SendAsync`:

```csharp
    private readonly Queue<(HttpStatusCode Status, string? Body, IEnumerable<(string Name, string Value)>? Headers)> _responses = new();

    public void Queue(
        HttpStatusCode status,
        string? body = null,
        IEnumerable<(string Name, string Value)>? headers = null) => _responses.Enqueue((status, body, headers));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var queued = _responses.Count > 0
            ? _responses.Dequeue()
            : (HttpStatusCode.OK, null, null);

        var body = request.Content is null
            ? Array.Empty<byte>()
            : await request.Content.ReadAsByteArrayAsync(cancellationToken);

        lock (_calls)
        {
            _calls.Add((request.RequestUri?.ToString() ?? string.Empty, body,
                request.Headers.Authorization?.ToString()));
        }

        var response = new HttpResponseMessage(queued.Item1);
        if (queued.Item2 is not null)
        {
            response.Content = new StringContent(queued.Item2, Encoding.UTF8, "application/json");
        }

        foreach (var (name, value) in queued.Item3 ?? [])
        {
            response.Headers.TryAddWithoutValidation(name, value);
        }

        return response;
    }
```

Existing `Queue(HttpStatusCode.OK)` calls still compile — the extra parameters are optional.

- [x] **Step 2: Write the failing test for the base's contract**

```csharp
[Fact]
public async Task CC49_ContentIsRebuiltPerAttempt_SoARetryPostsTheSameBodyAgain()
{
    // The pre-refactor senders reused one StringContent across attempts; its stream is consumed by
    // the first PostAsync, so the retry sent nothing.
    _recorder.Queue(HttpStatusCode.ServiceUnavailable);
    _recorder.Queue(HttpStatusCode.OK);

    var result = await CreateSut().SendAsync(Notification("15559998888", "retry me"));

    result.Succeeded.Should().BeTrue();
    _recorder.Calls.Should().HaveCount(2);
    Encoding.UTF8.GetString(_recorder.Calls[0].Body).Should().Contain("retry me");
    Encoding.UTF8.GetString(_recorder.Calls[1].Body).Should()
        .Be(Encoding.UTF8.GetString(_recorder.Calls[0].Body));
}
```

- [x] **Step 3: Run and watch it fail**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~CC49_ContentIsRebuilt"
```

Expected: FAIL — the second call's body is empty.

- [x] **Step 4: Write the base**

```csharp
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.ExternalApis.DTOs;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CustomerSupport.Infrastructure.Notifications.Channels;

/// <summary>
/// The transport half of every HTTP channel sender: configuration lookup, client construction,
/// auth, the bounded retry policy (NG-3/NG-4) and result mapping. A subclass supplies only its
/// provider's payload and how to read a message id out of the response (CC-49).
///
/// The credential arrives already decrypted from IExternalApiConfigurationProvider — see CC-51.
/// </summary>
public abstract class ChannelHttpSender : INotificationChannelSender
{
    private readonly IExternalApiConfigurationProvider _configProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    protected ChannelHttpSender(
        IExternalApiConfigurationProvider configProvider,
        IHttpClientFactory httpClientFactory,
        ILogger logger)
    {
        _configProvider = configProvider;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public abstract NotificationChannel SupportedChannel { get; }

    /// <summary>The `ExternalApiConfiguration` name this channel reads, e.g. `WhatsAppGateway`.</summary>
    protected abstract string ConfigName { get; }

    /// <summary>
    /// Built fresh per attempt — a consumed HttpContent cannot be re-posted, which silently made
    /// the old retry loop send an empty body.
    /// </summary>
    protected abstract HttpContent BuildContent(RenderedNotification notification);

    /// <summary>
    /// The provider's own id for the accepted message. Null when the provider gave none; never a
    /// fabricated value, because a fabricated id cannot be reconciled against a provider dashboard
    /// and defeats the (Channel, ProviderMessageId) idempotency index.
    /// </summary>
    protected abstract Task<string?> ReadProviderMessageIdAsync(
        HttpResponseMessage response, CancellationToken ct);

    public async Task<ChannelSendResult> SendAsync(
        RenderedNotification notification, CancellationToken ct = default)
    {
        var channel = SupportedChannel;

        var config = _configProvider.GetConfig(ConfigName);
        if (config is null)
        {
            _logger.LogWarning("Channel gateway configuration '{Config}' is missing", ConfigName);
            return new ChannelSendResult(channel, false, ApplicationErrors.Notification.CONFIG_MISSING);
        }

        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, config.TimeoutSeconds));
        ApplyAuth(client, config.Auth);

        for (var attempt = 1; attempt <= NotificationGatewayConstants.TransientRetryCount; attempt++)
        {
            try
            {
                using var content = BuildContent(notification);
                using var response = await client.PostAsync(config.BaseUrl, content, ct);

                if (response.IsSuccessStatusCode)
                {
                    var providerId = await ReadProviderMessageIdAsync(response, ct);
                    return new ChannelSendResult(channel, true, ProviderMessageId: providerId);
                }

                if (!IsTransient(response.StatusCode))
                {
                    _logger.LogWarning(
                        "{Channel} gateway returned {StatusCode} (non-transient)",
                        channel.Value, (int)response.StatusCode);
                    return new ChannelSendResult(channel, false, ApplicationErrors.Notification.DELIVERY_FAILED);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                _logger.LogWarning(ex, "{Channel} gateway transient failure on attempt {Attempt}",
                    channel.Value, attempt);
            }

            if (attempt < NotificationGatewayConstants.TransientRetryCount)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), ct);
            }
        }

        return new ChannelSendResult(channel, false, ApplicationErrors.Notification.DELIVERY_FAILED);
    }

    protected static HttpContent JsonContent(object payload) =>
        new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    /// <summary>Reads a top-level string property out of a JSON response body, or null.</summary>
    protected static async Task<string?> ReadJsonStringAsync(
        HttpResponseMessage response, string propertyName, CancellationToken ct)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return document.RootElement.TryGetProperty(propertyName, out var value)
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void ApplyAuth(HttpClient client, ExternalApiAuthConfig auth)
    {
        // Already decrypted by the configuration provider (CC-51).
        switch (auth.Type)
        {
            case ExternalApiAuthType.ApiKey when !string.IsNullOrWhiteSpace(auth.Value):
                client.DefaultRequestHeaders.Remove(auth.KeyName);
                client.DefaultRequestHeaders.Add(auth.KeyName, auth.Value);
                break;
            case ExternalApiAuthType.Bearer when !string.IsNullOrWhiteSpace(auth.Token):
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", auth.Token);
                break;
            case ExternalApiAuthType.Basic:
                var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{auth.ClientId}:{auth.ClientSecret}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
                break;
            default:
                break;
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is >= HttpStatusCode.InternalServerError or HttpStatusCode.RequestTimeout;

    private static bool IsTransient(Exception ex) =>
        ex is TimeoutException or HttpRequestException or OperationCanceledException;
}
```

- [x] **Step 5: Reduce `WhatsAppNotificationChannelSender` to an adapter**

Keep the class name and namespace so `ServiceCollectionExtensions.cs:80` needs no edit. Task 5
replaces the id extraction; for now keep behaviour identical apart from the base.

```csharp
public sealed class WhatsAppNotificationChannelSender : ChannelHttpSender
{
    public WhatsAppNotificationChannelSender(
        IExternalApiConfigurationProvider configProvider,
        IHttpClientFactory httpClientFactory,
        ILogger<WhatsAppNotificationChannelSender> logger)
        : base(configProvider, httpClientFactory, logger)
    {
    }

    public override NotificationChannel SupportedChannel => NotificationChannel.WhatsApp;

    protected override string ConfigName => NotificationGatewayConstants.WhatsAppGatewayConfigName;

    protected override HttpContent BuildContent(RenderedNotification notification) =>
        JsonContent(new
        {
            messaging_product = "whatsapp",
            to = notification.PhoneNumber,
            type = "text",
            text = new { body = notification.Message },
        });

    protected override Task<string?> ReadProviderMessageIdAsync(
        HttpResponseMessage response, CancellationToken ct) => Task.FromResult<string?>(null);
}
```

Do the same for `EmailNotificationChannelSender` (payload
`new { to = notification.Email, subject = notification.Title, body = notification.Message }`,
config name `EmailGatewayConfigName`, channel `NotificationChannel.Email`) and
`SmsNotificationChannelSender` (payload
`new { to = notification.PhoneNumber, body = notification.Message }`, config name
`SmsGatewayConfigName`, channel `NotificationChannel.Sms`). Tasks 6 and 7 replace those payloads.

- [x] **Step 6: Run the tests**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~WhatsAppNotificationChannelSenderTests|FullyQualifiedName~NotificationGatewayTests|FullyQualifiedName~WhatsAppOutboundReply"
```

Expected: PASS, including the new retry-body test. `CC6_SendAsync_RestoresTheBearerCredentialOnlyAtTheTransportBoundary`
now asserts a plaintext token rather than an unprotected one — update its expectation and note the
change in the task record.

- [ ] **Step 7: Full suite, then commit** — **not executed**, same instruction as Tasks 1–3.
  Targeted run instead: `WhatsAppNotificationChannelSenderTests` +`NotificationGatewayTests`
  +`WhatsAppOutboundReplyTests`+`WhatsAppWebhookTests` = **20/20 green**.

> **`CC49_ContentIsRebuiltPerAttempt` passed *before* the base class existed.** .NET's
> `StringContent`/`ByteArrayContent` buffers in memory and is safe to POST multiple times — the
> "consumed stream" failure mode the plan predicted doesn't reproduce for a JSON string payload
> (it would for a true forward-only `Stream`, which none of these three channels use). The base
> class still rebuilds content per attempt as designed, and is kept for the two things it does fix
> for real: the ~90% file-level duplication, and the fabricated provider id (`$"wa:{Guid.NewGuid()}"`)
> — the real bug, still present until Task 5 wires the actual `messages[0].id` read.

---

## Task 5: WhatsApp reads Meta's real message id (`CC-35`, `CC-39`)

**Files:**
- Modify: `backend/src/CustomerSupport.Infrastructure/Notifications/WhatsAppNotificationChannelSender.cs`
- Test: `backend/tests/CustomerSupport.Tests/Unit/Notifications/WhatsAppNotificationChannelSenderTests.cs`

**Interfaces:** Consumes `ChannelHttpSender`. Produces nothing new.

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task CC35_ProviderMessageId_IsReadFromMetasResponse()
{
    _recorder.Queue(
        HttpStatusCode.OK,
        body: """
        {
          "messaging_product": "whatsapp",
          "contacts": [ { "input": "15559998888", "wa_id": "15559998888" } ],
          "messages": [ { "id": "wamid.HBgLMTU1NTk5OTg4ODgVAgARGBI5QTND" } ]
        }
        """);

    var result = await CreateSut().SendAsync(Notification());

    result.Succeeded.Should().BeTrue();
    result.ProviderMessageId.Should().Be("wamid.HBgLMTU1NTk5OTg4ODgVAgARGBI5QTND");
}

[Fact]
public async Task CC39_PayloadIsExactlyMetaCloudApisShape()
{
    _recorder.Queue(HttpStatusCode.OK, body: """{"messages":[{"id":"wamid.X"}]}""");

    await CreateSut().SendAsync(Notification("15559998888", "Your bill is ready."));

    using var document = JsonDocument.Parse(_recorder.Calls[0].Body);
    var root = document.RootElement;
    root.GetProperty("messaging_product").GetString().Should().Be("whatsapp");
    root.GetProperty("to").GetString().Should().Be("15559998888");
    root.GetProperty("type").GetString().Should().Be("text");
    root.GetProperty("text").GetProperty("body").GetString().Should().Be("Your bill is ready.");
    root.EnumerateObject().Select(p => p.Name).Should()
        .BeEquivalentTo(["messaging_product", "to", "type", "text"]);
}
```

- [x] **Step 2: Run and watch it fail**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~CC35_ProviderMessageId|FullyQualifiedName~CC39_PayloadIsExactlyMeta"
```

Expected: FAIL — `ProviderMessageId` is null (Task 4 stubbed it).

- [x] **Step 3: Read `messages[0].id`**

```csharp
    protected override async Task<string?> ReadProviderMessageIdAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            return document.RootElement.TryGetProperty("messages", out var messages)
                && messages.ValueKind == JsonValueKind.Array
                && messages.GetArrayLength() > 0
                && messages[0].TryGetProperty("id", out var id)
                    ? id.GetString()
                    : null;
        }
        catch (JsonException)
        {
            // A 2xx with a body we cannot parse is still a successful send; the id is simply
            // unknown. Never fabricate one (CC-49).
            return null;
        }
    }
```

- [x] **Step 4: Run the tests**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~WhatsAppNotificationChannelSenderTests"
```

Expected: PASS.

- [ ] **Step 5: Commit** — **not executed**, same instruction. 11/11 targeted tests green.

---

## Task 6: Email speaks SendGrid v3 (`CC-34`, `CC-39`)

**Files:**
- Modify: `backend/src/CustomerSupport.Infrastructure/Notifications/EmailNotificationChannelSender.cs`
- Test: `backend/tests/CustomerSupport.Tests/Unit/Notifications/EmailNotificationChannelSenderTests.cs` (new file)

**Interfaces:** Consumes `ChannelHttpSender`, `ChannelOptions.EmailFrom`. The sender takes
`IOptions<ChannelOptions>` as a fourth constructor parameter — DI resolves it from Task 3's
`services.Configure<ChannelOptions>`.

- [x] **Step 1: Write the failing test**

New file, mirroring the WhatsApp test's fixtures (`RecordingHttpMessageHandler`,
`FakeHttpClientFactory` are `public` in `CustomerSupport.Tests.Unit.Notifications`, so reuse them
rather than copying).

```csharp
using System.Net;
using System.Text.Json;
using CustomerSupport.Application.Common.Options;
using CustomerSupport.Application.ExternalApis.DTOs;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.ValueObjects;
using CustomerSupport.Infrastructure.Notifications;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Notifications;

public class EmailNotificationChannelSenderTests
{
    private const string ApiBaseUrl = "http://localhost:3001/mock/sendgrid/v3/mail/send";

    private readonly Mock<IExternalApiConfigurationProvider> _configProvider = new();
    private readonly RecordingHttpMessageHandler _recorder = new();

    public EmailNotificationChannelSenderTests()
    {
        _configProvider
            .Setup(p => p.GetConfig(NotificationGatewayConstants.EmailGatewayConfigName))
            .Returns(new ExternalApiConfig { BaseUrl = ApiBaseUrl, TimeoutSeconds = 30 });
    }

    private EmailNotificationChannelSender CreateSut() =>
        new(
            _configProvider.Object,
            new FakeHttpClientFactory(_recorder.Client),
            Options.Create(new ChannelOptions { EmailFrom = "support@commandcenter.local" }),
            NullLogger<EmailNotificationChannelSender>.Instance);

    private static RenderedNotification Notification() =>
        new(null, "customer@example.com", null, "Ticket TKT-001001 updated",
            "Your ticket moved to Resolved.", "TICKET_REPLY", NotificationChannel.Email, null);

    [Fact]
    public async Task CC39_PayloadIsExactlySendGridV3sShape()
    {
        _recorder.Queue(HttpStatusCode.Accepted, headers: [("X-Message-Id", "sg-abc123")]);

        await CreateSut().SendAsync(Notification());

        using var document = JsonDocument.Parse(_recorder.Calls[0].Body);
        var root = document.RootElement;

        root.GetProperty("personalizations")[0].GetProperty("to")[0]
            .GetProperty("email").GetString().Should().Be("customer@example.com");
        root.GetProperty("from").GetProperty("email").GetString().Should().Be("support@commandcenter.local");
        root.GetProperty("subject").GetString().Should().Be("Ticket TKT-001001 updated");
        root.GetProperty("content")[0].GetProperty("type").GetString().Should().Be("text/plain");
        root.GetProperty("content")[0].GetProperty("value").GetString()
            .Should().Be("Your ticket moved to Resolved.");
    }

    [Fact]
    public async Task CC34_MessageIdComesFromTheXMessageIdHeaderOfAn202WithNoBody()
    {
        // SendGrid answers 202 Accepted with an empty body; the id is a header.
        _recorder.Queue(HttpStatusCode.Accepted, headers: [("X-Message-Id", "sg-abc123")]);

        var result = await CreateSut().SendAsync(Notification());

        result.Succeeded.Should().BeTrue();
        result.ProviderMessageId.Should().Be("sg-abc123");
    }
}
```

- [x] **Step 2: Run and watch it fail**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~EmailNotificationChannelSenderTests"
```

Expected: FAIL to compile — the constructor has no `IOptions<ChannelOptions>` parameter.

- [x] **Step 3: Implement the adapter**

```csharp
public sealed class EmailNotificationChannelSender : ChannelHttpSender
{
    private readonly ChannelOptions _options;

    public EmailNotificationChannelSender(
        IExternalApiConfigurationProvider configProvider,
        IHttpClientFactory httpClientFactory,
        IOptions<ChannelOptions> options,
        ILogger<EmailNotificationChannelSender> logger)
        : base(configProvider, httpClientFactory, logger)
    {
        _options = options.Value;
    }

    public override NotificationChannel SupportedChannel => NotificationChannel.Email;

    protected override string ConfigName => NotificationGatewayConstants.EmailGatewayConfigName;

    /// <summary>SendGrid v3 `POST /v3/mail/send`. `from` is required and had no equivalent in the
    /// house payload this replaces, so it comes from `Channels:EmailFrom`.</summary>
    protected override HttpContent BuildContent(RenderedNotification notification) =>
        JsonContent(new
        {
            personalizations = new[]
            {
                new { to = new[] { new { email = notification.Email } } },
            },
            from = new { email = _options.EmailFrom },
            subject = notification.Title,
            content = new[]
            {
                new { type = "text/plain", value = notification.Message },
            },
        });

    /// <summary>SendGrid returns 202 with an empty body; the id is in `X-Message-Id`.</summary>
    protected override Task<string?> ReadProviderMessageIdAsync(
        HttpResponseMessage response, CancellationToken ct) =>
        Task.FromResult(response.Headers.TryGetValues("X-Message-Id", out var values)
            ? values.FirstOrDefault()
            : null);
}
```

- [x] **Step 4: Run the tests**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~EmailNotificationChannelSenderTests"
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketCreatedNotification|FullyQualifiedName~SlaNotification|FullyQualifiedName~OtpRequest"
```

Expected: PASS. Those three integration suites dispatch email; they must not care about the payload
shape, and if one asserts the old `{to,subject,body}` body, update the assertion and record it.

- [ ] **Step 5: Commit** — **not executed**, same instruction. 9/9 targeted tests green
  (`EmailNotificationChannelSenderTests`, `TicketCreatedNotificationTests`, `OtpRequest*`).
  `SlaNotificationTests`' 3 failures confirmed pre-existing (present in Task 1's baseline, unrelated
  to email — the same "Sequence contains no elements" seeding pattern as `TicketMessagesEndpointTests`).

---

## Task 7: SMS speaks Twilio (`CC-36`, `CC-39`)

**Files:**
- Modify: `backend/src/CustomerSupport.Infrastructure/Notifications/SmsNotificationChannelSender.cs`
- Test: `backend/tests/CustomerSupport.Tests/Unit/Notifications/SmsNotificationChannelSenderTests.cs` (new file)

**Interfaces:** Consumes `ChannelHttpSender`, `ChannelOptions.SmsFrom`, and
`ChannelHttpSender.ReadJsonStringAsync`.

- [x] **Step 1: Write the failing test**

```csharp
    [Fact]
    public async Task CC39_BodyIsFormEncodedWithTwiliosFieldNames()
    {
        _recorder.Queue(HttpStatusCode.Created, body: """{"sid":"SM1234567890","status":"queued"}""");

        await CreateSut().SendAsync(Notification());

        var raw = Encoding.UTF8.GetString(_recorder.Calls[0].Body);
        var fields = System.Web.HttpUtility.ParseQueryString(raw);

        fields["To"].Should().Be("+15559998888");
        fields["From"].Should().Be("CommandCenter");
        fields["Body"].Should().Be("Your ticket moved to Resolved.");
    }

    [Fact]
    public async Task CC36_ProviderMessageIdIsTheTwilioSid()
    {
        _recorder.Queue(HttpStatusCode.Created, body: """{"sid":"SM1234567890","status":"queued"}""");

        var result = await CreateSut().SendAsync(Notification());

        result.Succeeded.Should().BeTrue();
        result.ProviderMessageId.Should().Be("SM1234567890");
    }
```

Fixtures mirror Task 6's file: `Notification()` returns a `RenderedNotification` with
`PhoneNumber: "+15559998888"`, `Message: "Your ticket moved to Resolved."`,
`Channel: NotificationChannel.Sms`; `CreateSut()` passes
`Options.Create(new ChannelOptions { SmsFrom = "CommandCenter" })`. `HttpUtility` needs
`<FrameworkReference Include="Microsoft.AspNetCore.App" />` — already present in the test project;
if not, parse with `raw.Split('&')` instead rather than adding a reference.

- [x] **Step 2: Run and watch it fail**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~SmsNotificationChannelSenderTests"
```

Expected: FAIL — the body is JSON, not form-encoded.

- [x] **Step 3: Implement the adapter**

```csharp
    /// <summary>Twilio's `POST /2010-04-01/Accounts/{sid}/Messages.json` takes form encoding, not
    /// JSON — the one channel here that is not a JSON API.</summary>
    protected override HttpContent BuildContent(RenderedNotification notification) =>
        new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = notification.PhoneNumber ?? string.Empty,
            ["From"] = _options.SmsFrom,
            ["Body"] = notification.Message,
        });

    protected override Task<string?> ReadProviderMessageIdAsync(
        HttpResponseMessage response, CancellationToken ct) =>
        ReadJsonStringAsync(response, "sid", ct);
```

- [x] **Step 4: Run the tests, then the full suite**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~SmsNotificationChannelSenderTests"
cd backend && dotnet test CustomerSupport.slnx
```

- [ ] **Step 5: Commit** — **not executed**, same instruction. `System.Web.HttpUtility` compiled
  fine with no extra framework reference needed. 2/2 new tests green; all three senders' tests
  together = 19/19.

---

## Task 8: Retry semantics against provider status codes (`CC-37`, `CC-38`)

The base already distinguishes transient from permanent; this task proves it per channel now that
the providers' real codes are in play (SendGrid `202`, Twilio `201`, Meta `200`; `429` and `5xx`
transient; `4xx` otherwise permanent).

**Files:**
- Modify: `backend/src/CustomerSupport.Infrastructure/Notifications/Channels/ChannelHttpSender.cs`
- Test: the three sender test files

**Interfaces:** none new.

- [x] **Step 1: Write the failing tests**

```csharp
    [Fact]
    public async Task CC38_TooManyRequests_IsTreatedAsTransientAndRetried()
    {
        _recorder.Queue(HttpStatusCode.TooManyRequests);
        _recorder.Queue(HttpStatusCode.Accepted, headers: [("X-Message-Id", "sg-after-retry")]);

        var result = await CreateSut().SendAsync(Notification());

        result.Succeeded.Should().BeTrue();
        result.ProviderMessageId.Should().Be("sg-after-retry");
        _recorder.Calls.Should().HaveCount(2);
    }

    [Fact]
    public async Task CC37_BadRequest_IsNeverRetried()
    {
        _recorder.Queue(HttpStatusCode.BadRequest);

        var result = await CreateSut().SendAsync(Notification());

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ApplicationErrors.Notification.DELIVERY_FAILED);
        _recorder.Calls.Should().HaveCount(1);
    }
```

Add both to the Email test file, and the `CC37_` one to the SMS and WhatsApp files.

- [x] **Step 2: Run and watch the 429 case fail**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~CC38_TooManyRequests|FullyQualifiedName~CC37_BadRequest"
```

Expected: `CC37_BadRequest` PASSES already; `CC38_TooManyRequests` FAILS — `429` is not in the
transient set, so it returns after one call.

- [x] **Step 3: Add 429 to the transient set**

```csharp
    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is >= HttpStatusCode.InternalServerError
            or HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests;
```

- [x] **Step 4: Run all three sender suites**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~NotificationChannelSenderTests"
```

Expected: PASS. `CC7_TransientFailure_RetriesUpToTheBoundedCount` must still cap at
`TransientRetryCount` (3).

- [ ] **Step 5: Commit** — **not executed**, same instruction. All three sender test files
  together = 18/18. WhatsApp's existing `CC7_PermanentFailure_IsNeverRetried` already covered
  `CC-37` there; no duplicate `CC37_` test added to that file.

---

## Task 9: Let a gateway model answer with a status code and headers

`middlewares/gateway-handler.js:50` ends every route with `res.json(response)` — always HTTP 200,
never a header. That blocks `CC-34` (SendGrid's `202` + `X-Message-Id`) and quietly breaks
`CC-37`/`CC-38`: the existing mocks return `{status:'failed'}` with HTTP 200, which the senders read
as **success**. Backward compatible — a model returning a plain object keeps getting 200 JSON.

**Files:**
- Modify: `cms-integration-gateway/middlewares/gateway-handler.js:33-50`
- Test: `cms-integration-gateway/scripts/test-response-envelope.js` (new)

**Interfaces:**
- Produces: the response envelope
  `{ $response: true, status: <number>, headers: { <name>: <value> }, body: <object|null> }`.
  Tasks 10–12 return it. The `$response` marker is explicit so it cannot collide with the existing
  transforms, which legitimately return a `status: 'success'` **string** field.

- [x] **Step 1: Write the failing check**

```javascript
// scripts/test-response-envelope.js — run against a started server.
const http = require('http');

function post(path, body, contentType = 'application/json') {
    return new Promise((resolve) => {
        const payload = contentType === 'application/json' ? JSON.stringify(body) : body;
        const req = http.request(
            { hostname: 'localhost', port: 3001, path, method: 'POST',
              headers: { 'Content-Type': contentType, 'Content-Length': Buffer.byteLength(payload) } },
            (res) => {
                let data = '';
                res.on('data', (c) => (data += c));
                res.on('end', () => resolve({ status: res.statusCode, headers: res.headers, body: data }));
            });
        req.write(payload);
        req.end();
    });
}

(async () => {
    const sendgrid = await post('/mock/sendgrid/v3/mail/send', {
        personalizations: [{ to: [{ email: 'customer@example.com' }] }],
        from: { email: 'support@commandcenter.local' },
        subject: 'Hello',
        content: [{ type: 'text/plain', value: 'Body' }],
    });

    const checks = [
        ['sendgrid status is 202', sendgrid.status === 202],
        ['sendgrid sets x-message-id', Boolean(sendgrid.headers['x-message-id'])],
        ['sendgrid body is empty', sendgrid.body === ''],
        ['legacy sms route still answers 200 json',
            (await post('/integrationgateway/sms/send', { to: '+966501234567', body: 'hi' })).status === 200],
    ];

    let failed = 0;
    for (const [name, ok] of checks) {
        console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}`);
        if (!ok) failed += 1;
    }
    process.exit(failed === 0 ? 0 : 1);
})();
```

Add to `package.json` scripts: `"test:envelope": "node scripts/test-response-envelope.js"`.

- [x] **Step 2: Run it and watch it fail**

```bash
cd cms-integration-gateway && npm start &
sleep 3 && npm run test:envelope
```

Expected: the three SendGrid checks FAIL (route does not exist yet), the legacy check PASSES. Stop
the server afterwards.

- [x] **Step 3: Teach the handler the envelope**

Replace the `res.json(response)` tail (line 50) and the realtime block (44-48):

```javascript
                    // Real-time broadcast for send endpoints. `realtimeType` lets the new
                    // provider-shaped routes label themselves; the old path sniffing stays for
                    // the existing /integrationgateway/* routes.
                    const isSend = endpoint.path.includes('/send')
                        || endpoint.path.includes('/messages')
                        || endpoint.path.includes('/mail');
                    if (storeRealtimeMessage && isSend) {
                        const type = endpoint.realtimeType
                            || (endpoint.path.includes('/sms/') ? 'sms'
                                : endpoint.path.includes('/email/') ? 'email'
                                : 'unknown');
                        storeRealtimeMessage(type, req.body || {}, response);
                    }

                    // A model may answer with a real status code and headers by returning the
                    // envelope below. Anything else keeps the historical behaviour: 200 + JSON.
                    if (response && response.$response === true) {
                        if (response.headers) {
                            Object.entries(response.headers).forEach(([name, value]) => res.set(name, value));
                        }
                        const status = response.status || 200;
                        if (response.body === null || response.body === undefined) {
                            return res.status(status).end();
                        }
                        return res.status(status).json(response.body);
                    }

                    res.json(response);
```

- [x] **Step 4: Re-run after Task 10 lands**

The SendGrid checks stay red until Task 10 adds the route; that is expected and is why Task 10
follows immediately. Confirm now only that the legacy check still passes:

```bash
cd cms-integration-gateway && npm start &
sleep 3 && npm run test:envelope; # legacy line must read PASS
```

- [ ] **Step 5: Commit** — **not executed**, same instruction as all prior tasks.

```bash
git add cms-integration-gateway/middlewares/gateway-handler.js cms-integration-gateway/scripts cms-integration-gateway/package.json
git commit -m "feat(gateway): let a model answer with a status code, headers and an empty body"
```

---

## Task 10: SendGrid mock (`CC-34`)

**Files:**
- Create: `cms-integration-gateway/models/SendGridGatewayModel.js`
- Create: `cms-integration-gateway/behaviors/provider-failure-rules.js`
- Create: `cms-integration-gateway/mocks/providers/history.json` (contents: `[]`)
- Modify: `cms-integration-gateway/models/ServiceRegistry.js`

**Interfaces:**
- Produces: `provider-failure-rules` exporting
  `check(recipient) → { kind: 'permanent'|'transient', code, message } | null`, used by Tasks 11–12
  too. Reserved recipients, documented in one place:
  - `permanent-fail@mock.test` / `+19995550000` → permanent
  - `transient-fail@mock.test` / `+19995550001` → transient twice, then success

- [x] **Step 1: Write the shared failure rules**

```javascript
/**
 * Deterministic failure triggers for the provider mocks (CC-37/CC-38).
 *
 * Deterministic, not random: the backend's bounded-retry policy can only be asserted end-to-end if
 * the same recipient fails the same way every run. The existing sms/email mocks randomise their
 * status, which cannot support that.
 */
const PERMANENT = new Set(['permanent-fail@mock.test', '+19995550000']);
const TRANSIENT = new Set(['transient-fail@mock.test', '+19995550001']);

const TRANSIENT_FAILURES_BEFORE_SUCCESS = 2;
const attempts = new Map();

module.exports = {
    /** @returns {{kind: 'permanent'|'transient', code: string, message: string}|null} */
    check: (recipient) => {
        const key = String(recipient || '').trim();

        if (PERMANENT.has(key)) {
            return { kind: 'permanent', code: 'INVALID_RECIPIENT', message: `Recipient ${key} is not deliverable` };
        }

        if (TRANSIENT.has(key)) {
            const soFar = attempts.get(key) || 0;
            if (soFar < TRANSIENT_FAILURES_BEFORE_SUCCESS) {
                attempts.set(key, soFar + 1);
                return { kind: 'transient', code: 'UPSTREAM_UNAVAILABLE', message: 'Temporarily unavailable' };
            }
            attempts.delete(key);
        }

        return null;
    },

    /** Test hook — clears the transient counters so a suite can re-run from a known state. */
    reset: () => attempts.clear(),
};
```

- [x] **Step 2: Write the SendGrid model**

```javascript
/**
 * @swagger
 * tags:
 *   name: SendGrid
 *   description: SendGrid v3 mail/send mock (CC-34)
 */
const { v4: uuidv4 } = require('uuid');

module.exports = {
    name: 'sendgrid-gateway',
    group: 'mock',
    description: 'SendGrid v3 mock — impersonates POST /v3/mail/send',
    endpoints: [
        {
            path: '/mock/sendgrid/v3/mail/send',
            method: 'POST',
            mockDataKey: 'providers-history',
            behaviorKey: 'provider-failure-rules',
            realtimeType: 'email',
            description: 'Send an email (SendGrid v3 contract)',
            responseTransform: (req, mockData, rules) => {
                const payload = req.body || {};
                const to = payload?.personalizations?.[0]?.to?.[0]?.email || null;

                if (!to || !payload.from?.email || !payload.subject) {
                    // SendGrid's real validation envelope.
                    return {
                        $response: true,
                        status: 400,
                        body: { errors: [{ message: 'missing required field', field: 'personalizations.to' }] },
                    };
                }

                const failure = rules ? rules.check(to) : null;
                if (failure) {
                    return {
                        $response: true,
                        status: failure.kind === 'permanent' ? 400 : 503,
                        body: { errors: [{ message: failure.message, field: null, help: failure.code }] },
                    };
                }

                // 202 Accepted, empty body, id in a header — the real contract.
                return {
                    $response: true,
                    status: 202,
                    headers: { 'X-Message-Id': `sg-${uuidv4()}` },
                    body: null,
                };
            },
        },
    ],
};
```

- [x] **Step 3: Register it**

In `models/ServiceRegistry.js`, add the require beside the others and `register(sendGridModel);`
below the existing registrations.

- [x] **Step 4: Run the envelope check**

```bash
cd cms-integration-gateway && npm start &
sleep 3 && npm run test:envelope
```

Expected: all four checks PASS.

- [ ] **Step 5: Commit** — **not executed**, same instruction as all prior tasks.

```bash
git add cms-integration-gateway
git commit -m "feat(gateway): SendGrid v3 mock with deterministic failure triggers (CC-34)"
```

---

## Task 11: Meta WhatsApp mock (`CC-35`)

**Files:**
- Create: `cms-integration-gateway/models/MetaWhatsAppGatewayModel.js`
- Modify: `cms-integration-gateway/models/ServiceRegistry.js`
- Modify: `cms-integration-gateway/scripts/test-response-envelope.js` (add a WhatsApp check)

- [x] **Step 1: Add the failing check**

```javascript
    const meta = await post('/mock/meta/v18.0/100000000000000/messages', {
        messaging_product: 'whatsapp', to: '+15559998888', type: 'text', text: { body: 'hi' },
    });
    checks.push(
        ['meta answers 200', meta.status === 200],
        ['meta returns a wamid', /^wamid\./.test(JSON.parse(meta.body || '{}')?.messages?.[0]?.id || '')],
    );
```

- [x] **Step 2: Run it and watch it fail** — `npm run test:envelope`, expected FAIL on both.

- [x] **Step 3: Write the model**

```javascript
/**
 * @swagger
 * tags:
 *   name: WhatsApp
 *   description: Meta WhatsApp Cloud API mock (CC-35)
 */
const { v4: uuidv4 } = require('uuid');

module.exports = {
    name: 'meta-whatsapp-gateway',
    group: 'mock',
    description: 'Meta Cloud API v18 mock — impersonates POST /{phone-number-id}/messages',
    endpoints: [
        {
            path: '/mock/meta/v18.0/:phoneNumberId/messages',
            method: 'POST',
            mockDataKey: 'providers-history',
            behaviorKey: 'provider-failure-rules',
            realtimeType: 'whatsapp',
            description: 'Send a WhatsApp message (Cloud API contract)',
            responseTransform: (req, mockData, rules) => {
                const payload = req.body || {};

                if (payload.messaging_product !== 'whatsapp' || !payload.to) {
                    // Meta's real error envelope.
                    return {
                        $response: true,
                        status: 400,
                        body: {
                            error: {
                                message: '(#100) Invalid parameter',
                                type: 'OAuthException',
                                code: 100,
                                fbtrace_id: uuidv4(),
                            },
                        },
                    };
                }

                const failure = rules ? rules.check(payload.to) : null;
                if (failure) {
                    return {
                        $response: true,
                        status: failure.kind === 'permanent' ? 400 : 503,
                        body: {
                            error: {
                                message: failure.message,
                                type: 'OAuthException',
                                code: failure.kind === 'permanent' ? 131026 : 500,
                                fbtrace_id: uuidv4(),
                            },
                        },
                    };
                }

                return {
                    $response: true,
                    status: 200,
                    body: {
                        messaging_product: 'whatsapp',
                        contacts: [{ input: payload.to, wa_id: String(payload.to).replace(/\D/g, '') }],
                        messages: [{ id: `wamid.${Buffer.from(uuidv4()).toString('base64').replace(/=+$/, '')}` }],
                    },
                };
            },
        },
    ],
};
```

- [x] **Step 4: Register, run, expect PASS.**

- [ ] **Step 5: Commit** — **not executed**, same instruction as all prior tasks.

```bash
git add cms-integration-gateway
git commit -m "feat(gateway): Meta WhatsApp Cloud API mock (CC-35)"
```

---

## Task 12: Twilio mock (`CC-36`)

**Files:**
- Create: `cms-integration-gateway/models/TwilioGatewayModel.js`
- Modify: `cms-integration-gateway/models/ServiceRegistry.js`
- Modify: `cms-integration-gateway/scripts/test-response-envelope.js`

- [x] **Step 1: Add the failing check** — note the form encoding, not JSON:

```javascript
    const twilio = await post(
        '/mock/twilio/2010-04-01/Accounts/ACmockaccountsid/Messages.json',
        'To=%2B15559998888&From=CommandCenter&Body=hi',
        'application/x-www-form-urlencoded');
    checks.push(
        ['twilio answers 201', twilio.status === 201],
        ['twilio returns an SM sid', /^SM/.test(JSON.parse(twilio.body || '{}').sid || '')],
    );
```

- [x] **Step 2: Run it and watch it fail.**

- [x] **Step 3: Write the model**

`express.urlencoded({ extended: true })` is already registered (`server.js:203`), so `req.body`
arrives parsed.

```javascript
/**
 * @swagger
 * tags:
 *   name: SMS
 *   description: Twilio Messages mock (CC-36)
 */
const { v4: uuidv4 } = require('uuid');

module.exports = {
    name: 'twilio-gateway',
    group: 'mock',
    description: 'Twilio mock — impersonates POST /2010-04-01/Accounts/{sid}/Messages.json',
    endpoints: [
        {
            path: '/mock/twilio/2010-04-01/Accounts/:accountSid/Messages.json',
            method: 'POST',
            mockDataKey: 'providers-history',
            behaviorKey: 'provider-failure-rules',
            realtimeType: 'sms',
            description: 'Send an SMS (Twilio contract, form-encoded)',
            responseTransform: (req, mockData, rules) => {
                // Twilio takes application/x-www-form-urlencoded with capitalised field names.
                const to = req.body?.To;
                const from = req.body?.From;
                const body = req.body?.Body;

                if (!to || !body) {
                    return {
                        $response: true,
                        status: 400,
                        body: {
                            code: 21604,
                            message: "A 'To' phone number and 'Body' are required",
                            more_info: 'https://www.twilio.com/docs/errors/21604',
                            status: 400,
                        },
                    };
                }

                const failure = rules ? rules.check(to) : null;
                if (failure) {
                    return {
                        $response: true,
                        status: failure.kind === 'permanent' ? 400 : 503,
                        body: {
                            code: failure.kind === 'permanent' ? 21211 : 20500,
                            message: failure.message,
                            more_info: 'https://www.twilio.com/docs/errors',
                            status: failure.kind === 'permanent' ? 400 : 503,
                        },
                    };
                }

                const sid = `SM${uuidv4().replace(/-/g, '')}`;
                return {
                    $response: true,
                    status: 201,
                    body: {
                        sid,
                        account_sid: req.params.accountSid,
                        to,
                        from: from || 'CommandCenter',
                        body,
                        status: 'queued',
                        num_segments: String(Math.ceil(String(body).length / 160)),
                        date_created: new Date().toUTCString(),
                        uri: `/2010-04-01/Accounts/${req.params.accountSid}/Messages/${sid}.json`,
                    },
                };
            },
        },
    ],
};
```

- [x] **Step 4: Register, run, expect all eight checks PASS.**

- [ ] **Step 5: Commit** — **not executed**, same instruction as all prior tasks.

```bash
git add cms-integration-gateway
git commit -m "feat(gateway): Twilio Messages mock, form-encoded (CC-36)"
```

---

## Task 13: End-to-end verification and documentation

**Files:**
- Modify: `cms-integration-gateway/.env.example`, `cms-integration-gateway/config.js`
- Modify: `cms-integration-gateway/CLAUDE.md` ("Current Services")
- Modify: `CLAUDE.md` (repo root, Commands table)
- Modify: `docs/superpowers/plans/EPIC-03-US-201-feat-35-channel-mock-gateway/README.md` (the record)

- [x] **Step 1: Add the two gateway settings**

`.env.example`:

```
# Where the mocks post inbound webhooks (plan 2). Must reach the ExternalApi host.
CALLBACK_BASE_URL=http://localhost:5095

# Shared with the backend's Channels:MockWebhookSecret so inbound signatures verify for real.
WEBHOOK_SECRET=dev-only-channel-webhook-secret
```

`config.js`, inside the `config` object:

```javascript
    CALLBACK_BASE_URL: process.env.CALLBACK_BASE_URL || 'http://localhost:5095',
    WEBHOOK_SECRET: process.env.WEBHOOK_SECRET || 'dev-only-channel-webhook-secret',
```

- [x] **Step 2: Prove the toggle end to end, by hand**

```bash
cd cms-integration-gateway && npm start &
sleep 3

cd backend && \
ConnectionStrings__DefaultConnection='Server=(localdb)\MSSQLLocalDB;Database=CustomerSupportCrm;Trusted_Connection=True;TrustServerCertificate=True' \
Jwt__Key='local-development-key-long-enough-to-pass-validation' \
Channels__UseMocks=true \
Serilog__Using__0=Serilog.Sinks.Console Serilog__WriteTo__0__Name=Console \
dotnet run --project src/CustomerSupport.InternalApi --urls http://localhost:5074
```

Reply to a ticket over WhatsApp through `POST /api/Tickets/{id}/messages`
(`{"direction":"Outbound","channel":"WhatsApp","body":"testing the mock"}`), then confirm:
- the gateway console logged `[Gateway] POST /mock/meta/v18.0/100000000000000/messages`
- the `NotificationDeliveries` row for that send has status Sent and a `wamid.`-prefixed provider id
- Mock Manager (`http://localhost:3001/mock-manager`) shows the message

Paste the observed provider id and the gateway log line into the record. **Do not** record this task
complete on the strength of the unit tests alone — the point of this step is the wire.

- [x] **Step 3: Prove the production guard**

```bash
cd backend && ASPNETCORE_ENVIRONMENT=Production Channels__UseMocks=true \
ConnectionStrings__DefaultConnection='...' Jwt__Key='...' \
dotnet run --project src/CustomerSupport.InternalApi
```

Expected: startup throws `Channels:UseMocks must not be true when the environment is Production`.
Paste the message.

- [x] **Step 4: Prove `CC-50` — the suite does not need the gateway**

```bash
# with NOTHING listening on 3001
cd backend && dotnet test CustomerSupport.slnx
```

Expected: the same counts as Task 8, with port 3001 closed.

- [x] **Step 5: Update the documentation**

`cms-integration-gateway/CLAUDE.md`, under "Current Services", add:

```markdown
### Provider-faithful channel mocks (FEAT-35)

Impersonate the real vendor contracts so the backend's `Channels:UseMocks` flag is a base-URL swap
rather than a second code path.

- `POST /mock/sendgrid/v3/mail/send` — SendGrid v3. Answers `202` with an empty body and an
  `X-Message-Id` header.
- `POST /mock/meta/v18.0/:phoneNumberId/messages` — Meta Cloud API. Answers `200` with
  `messages[0].id` as a `wamid.`.
- `POST /mock/twilio/2010-04-01/Accounts/:accountSid/Messages.json` — Twilio, **form-encoded**.
  Answers `201` with a `sid`.

Deterministic failure triggers (`behaviors/provider-failure-rules.js`):

| Recipient | Behaviour |
|---|---|
| `permanent-fail@mock.test`, `+19995550000` | permanent `4xx` — never retried by the backend |
| `transient-fail@mock.test`, `+19995550001` | `503` twice, then success — exercises the bounded retry |

A model may answer with a status code and headers by returning
`{ $response: true, status, headers, body }`; returning a plain object keeps the historical
`200 + JSON`.
```

Repo-root `CLAUDE.md`, in the Commands table:

```markdown
| Run mock channel gateway | `cd cms-integration-gateway && npm start` (port 3001) |
| Use the mocks | set `Channels__UseMocks=true` on either API host |
```

- [x] **Step 6a: Write the task record** — done, see the plan folder's `README.md`.
- [ ] **Step 6b: Commit** — not executed, same instruction as every other task this session
  (explicit no-commit for the whole implementation pass).

Create `README.md` in this plan folder with one row per task: criteria covered, commit hash, the
**test output actually observed**, and every deviation from this plan with its reason. Then:

```bash
git add cms-integration-gateway CLAUDE.md docs/superpowers/plans/EPIC-03-US-201-feat-35-channel-mock-gateway
git commit -m "docs: FEAT-35 plan 1 record — outbound channels through provider-faithful mocks"
```

---

## Self-review

**Spec coverage.** `CC-30` Task 3 · `CC-31` Task 3 · `CC-32` Task 3 · `CC-33` Task 3 · `CC-34`
Tasks 6, 10 · `CC-35` Tasks 5, 11 · `CC-36` Tasks 7, 12 · `CC-37` Tasks 8, 10–12 · `CC-38` Tasks 8,
10–12 · `CC-39` Tasks 5–7 · `CC-48` Task 2 · `CC-49` Task 4 · `CC-51` Task 1. No criterion in this
plan's range is unassigned. `CC-40`–`CC-47` and `CC-50` belong to plan 2, except `CC-50`'s
verification which Task 13 step 4 performs because this plan is what could break it.

**Type consistency.** `ChannelOptions` is the name throughout (never `ChannelMockOptions`).
`ChannelHttpSender`'s four abstract members are named identically in Tasks 4–7. The recorder's
`Queue(status, body, headers)` signature in Task 4 matches every call in Tasks 5–8. The gateway
envelope is `{ $response, status, headers, body }` in Tasks 9–12 without variation.

**Known sequencing constraint.** Task 9's envelope test stays partly red until Task 10 lands the
first route that uses it. That is stated in Task 9 step 4 rather than hidden, and is the reason
Tasks 9 and 10 are adjacent.

**Risk carried deliberately.** Task 1 changes a shared path that eight currently-green tests touch;
its step 5 runs the whole suite before the commit for exactly that reason. Task 6 changes the email
payload three integration suites exercise, and names them in its step 4.
