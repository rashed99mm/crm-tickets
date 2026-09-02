# FEAT-35 inbound completion — SMS, email and the web form: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the inbound half of the communication channels — a Twilio-shaped SMS webhook, a
SendGrid-Inbound-Parse-shaped email webhook, and the customer-portal web form — so all three land in
the shared ingestion path that WhatsApp already uses.

**Architecture:** Nothing new is invented. Each channel gets one thin controller on the
customer-facing `ExternalApi` host that parses its own provider payload and hands a normalized
`IngestInboundChannelMessageCommand` to the existing shared handler —
`WhatsAppWebhookController.cs` is the template all three copy. Signature verification grows a second
implementation (`TwilioSignatureVerifier`) plus a composite dispatcher behind the one
`IWebhookSignatureVerifier` slot, so neither existing controller changes shape. Two small
corrections to already-shipped code fall out of this: the ingestion command learns an optional
`Subject` (it discards one today), and the outbound reply branch learns to route email by address
instead of phone.

**Tech Stack:** .NET 10, C#, MediatR CQRS, FluentValidation, EF Core + SQL Server LocalDB,
xUnit + FluentAssertions + `WebApplicationFactory`; Node/Express for the mock gateway simulators.

**Spec:** [`docs/superpowers/specs/EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md`](../../specs/EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md)
— the **"Amendment — 2026-09-02, inbound completion"** section (assumptions `A20`–`A27`). Read it
alongside this plan; the plan argues from it and does not restate its reasoning.

## Global Constraints

- **The dependency rule does not bend.** `Domain` references nothing; `Application` references only
  `Domain`. `IWebhookSignatureVerifier` stays a pure port in `Application/Channels/Contracts.cs`
  with no ASP.NET types — controllers extract the raw bytes, the signature header and the URL and
  pass primitives in. New verifier implementations live in `Infrastructure/Channels/`.
- **No commits this session.** Every task's final step says so explicitly. Implement, verify, record
  — do not run `git commit`.
- **Every test names its criterion**, either in the method name (`CC40_...`) or via
  `[Trait("AC", "CC40")]`. Follow `WhatsAppWebhookTests.cs`, which does both.
- **Do not re-run the full suite per task.** The established pre-existing-failure baseline is
  **56 named failures** (see `EPIC-03-US-201-feat-35-channel-mock-gateway/README.md`, Task 13). Run
  targeted filters per task; cross-reference any incidental failure against
  `/tmp/final-failed-names.txt` before treating it as a regression. One full-suite run at Task 9.
- **New error codes need bilingual entries** in
  `backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml`, or
  `ContractHardeningTests.EveryErrorCode_HasABilingualMessage` fails. **This plan introduces no new
  error codes** — every code it uses already exists.
- **Channel names come from one place**: `CustomerSupport.Domain.Common.ChannelNames`
  (`Email`, `Sms` = `"SMS"`, `WebForm`, already in `ChannelNames.Inbound`). Never write the literal.
- Build must be clean under warnings-as-errors: `cd backend && dotnet build CustomerSupport.slnx`.

## File structure

**Created — backend production:**

| File | Responsibility |
|---|---|
| `backend/src/CustomerSupport.Infrastructure/Channels/TwilioSignatureVerifier.cs` | Twilio's HMAC-SHA1-over-URL+sorted-params check. Nothing else. |
| `backend/src/CustomerSupport.Infrastructure/Channels/CompositeWebhookSignatureVerifier.cs` | Routes a `Verify(provider, …)` call to whichever verifier handles that provider. |
| `backend/src/CustomerSupport.ExternalApi/Controllers/SmsWebhookController.cs` | Twilio inbound form payload → shared ingestion. |
| `backend/src/CustomerSupport.ExternalApi/Controllers/EmailWebhookController.cs` | SendGrid Inbound Parse multipart payload → shared ingestion. |
| `backend/src/CustomerSupport.ExternalApi/Controllers/WebFormController.cs` | Portal web-form submit: honeypot, throttle, ingestion, reference. |
| `backend/src/CustomerSupport.Application/Channels/IWebFormSubmissionThrottle.cs` | Port: "may this client submit again?" |
| `backend/src/CustomerSupport.Infrastructure/Channels/WebFormSubmissionThrottle.cs` | Per-IP fixed window, in memory, singleton. |
| `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTicketReferenceForMessage/GetTicketReferenceForMessageQuery.cs` | The `A25` read, as a query. |
| `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTicketReferenceForMessage/GetTicketReferenceForMessageQueryHandler.cs` | `TicketMessage.TicketId` → `Ticket.Reference`. |

**Modified — backend production:**

| File | Change |
|---|---|
| `backend/src/CustomerSupport.Infrastructure/ServiceCollectionExtensions.cs:107` | Register the composite (plus both verifiers and the throttle) instead of `MetaSignatureVerifier` directly. |
| `.../IngestInboundChannelMessage/IngestInboundChannelMessageCommand.cs` | Add optional `Subject` (`A23`). |
| `.../IngestInboundChannelMessageCommandHandler.cs:74` | Use `request.Subject` when present. |
| `.../IngestInboundChannelMessageCommandValidator.cs` | `Subject` max length 200. |
| `.../RecordTicketMessage/RecordTicketMessageCommandHandler.cs:69-96` | `A27`: route email by address, skip `@channel.invalid`. |

**Created — tests:** `Unit/Channels/TwilioSignatureVerifierTests.cs`,
`Unit/Channels/CompositeWebhookSignatureVerifierTests.cs`,
`Unit/Channels/WebFormSubmissionThrottleTests.cs`, `Integration/SmsInboundWebhookTests.cs`,
`Integration/EmailInboundWebhookTests.cs`, `Integration/EmailOutboundReplyTests.cs`,
`Integration/WebFormSubmissionTests.cs`.

**Modified — tests:** `Integration/GatewayTestData.cs` (add SMS + email gateway seeds),
`Integration/CrmExternalApiFactory.cs` and `Integration/CrmApiFactory.cs` (bridge methods for them).

**Created — gateway:** `cms-integration-gateway/scripts/simulate-sms-inbound.js`,
`cms-integration-gateway/scripts/simulate-email-inbound.js`.
**Modified — gateway:** `cms-integration-gateway/package.json` (two `npm run` entries),
`cms-integration-gateway/CLAUDE.md` (document them).

---

## Task 1: `TwilioSignatureVerifier`

**Criteria:** `CC-40`, `CC-41` (the verification half). Spec `A22`.

**Files:**
- Create: `backend/src/CustomerSupport.Infrastructure/Channels/TwilioSignatureVerifier.cs`
- Test: `backend/tests/CustomerSupport.Tests/Unit/Channels/TwilioSignatureVerifierTests.cs`

**Interfaces:**
- Consumes: `CustomerSupport.Application.Channels.IWebhookSignatureVerifier` —
  `bool Verify(string provider, string? signature, string? requestUrl, byte[] rawBody)`
  (`Application/Channels/Contracts.cs:27-36`); `IExternalApiConfigurationProvider.GetConfig(string)`
  returning `ExternalApiConfig?` with `.Auth.Value`;
  `NotificationGatewayConstants.SmsGatewayConfigName` = `"SmsGateway"`.
- Produces: `TwilioSignatureVerifier(IExternalApiConfigurationProvider configProvider)`, consumed by
  Task 2's composite.

**Why this shape:** `MetaSignatureVerifier.cs` is the precedent to copy line-for-line — same
constructor dependency, same "read the secret from the gateway config's `Auth.Value`, never
re-`Unprotect` it" comment (`DatabaseExternalApiProvider` already decrypted it; a second unprotect
is the `CC-51` bug), same `CryptographicOperations.FixedTimeEquals` comparison. Only three things
differ: the provider it answers for (`"SMS"`), the config it reads (`SmsGateway`), and the algorithm.

**Twilio's algorithm** (differs from Meta's in every particular — this is why `Verify` already
carries the `requestUrl` parameter Meta ignores): take the full request URL as Twilio was configured
with it, append each POST parameter's key immediately followed by its value, in **ordinal order by
key**, then HMAC-SHA1 the resulting string with the account auth token and **Base64**-encode it —
not hex, and not over the raw body.

- [x] **Step 1: Write the failing tests**

Create `backend/tests/CustomerSupport.Tests/Unit/Channels/TwilioSignatureVerifierTests.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using CustomerSupport.Application.ExternalApis.DTOs;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Infrastructure.Channels;
using FluentAssertions;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Channels;

/// <summary>
/// CC-40/CC-41 — Twilio's inbound signature scheme: HMAC-SHA1, Base64, over the request URL plus
/// alphabetically-ordered POST parameters. Deliberately unit-level: the algorithm is the whole risk
/// here, and it is a pure function of (url, body, secret).
/// </summary>
public class TwilioSignatureVerifierTests
{
    private const string AuthToken = "twilio-auth-token-for-tests-only";
    private const string Url = "https://support.example.com/api/channels/sms/webhook";

    private static TwilioSignatureVerifier CreateSut(string? secret = AuthToken)
    {
        var provider = new Mock<IExternalApiConfigurationProvider>();
        provider
            .Setup(p => p.GetConfig(NotificationGatewayConstants.SmsGatewayConfigName))
            .Returns(secret is null
                ? null
                : new ExternalApiConfig
                {
                    BaseUrl = "https://api.twilio.com",
                    TimeoutSeconds = 30,
                    Auth = new ExternalApiAuthConfig { Type = ExternalApiAuthType.None, Value = secret },
                });

        return new TwilioSignatureVerifier(provider.Object);
    }

    /// <summary>Twilio's documented recipe, written out independently of the implementation.</summary>
    private static string Sign(string secret, string url, params (string Key, string Value)[] form)
    {
        var payload = new StringBuilder(url);
        foreach (var (key, value) in form.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            payload.Append(key).Append(value);
        }

        using var mac = new HMACSHA1(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(mac.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString())));
    }

    private static byte[] FormBody(params (string Key, string Value)[] form) =>
        Encoding.UTF8.GetBytes(string.Join("&", form.Select(f =>
            $"{Uri.EscapeDataString(f.Key)}={Uri.EscapeDataString(f.Value)}")));

    [Fact]
    [Trait("AC", "CC40")]
    public void CC40_ValidTwilioSignature_IsAccepted()
    {
        var form = new[] { ("From", "+15559998888"), ("Body", "help me"), ("MessageSid", "SM123") };
        var signature = Sign(AuthToken, Url, form);

        CreateSut().Verify("SMS", signature, Url, FormBody(form)).Should().BeTrue();
    }

    [Fact]
    [Trait("AC", "CC40")]
    public void CC40_ParameterOrderInTheBody_DoesNotAffectTheResult()
    {
        // Twilio sorts by key when signing; the body's own order is arbitrary. A verifier that
        // hashed the body in wire order would pass the test above and fail in production.
        var signed = new[] { ("Body", "help me"), ("From", "+15559998888") };
        var signature = Sign(AuthToken, Url, signed);
        var shuffledBody = FormBody(("From", "+15559998888"), ("Body", "help me"));

        CreateSut().Verify("SMS", signature, Url, shuffledBody).Should().BeTrue();
    }

    [Fact]
    [Trait("AC", "CC41")]
    public void CC41_WrongSignature_IsRefused()
    {
        var form = new[] { ("From", "+15559998888"), ("Body", "forged") };
        var wrong = Sign("some-other-token", Url, form);

        CreateSut().Verify("SMS", wrong, Url, FormBody(form)).Should().BeFalse();
    }

    [Theory]
    [Trait("AC", "CC41")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CC41_MissingSignature_IsRefused(string? signature)
    {
        var form = new[] { ("From", "+15559998888"), ("Body", "unsigned") };

        CreateSut().Verify("SMS", signature, Url, FormBody(form)).Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "CC41")]
    public void CC41_DifferentUrl_IsRefused()
    {
        // The URL is part of the signed material, so a replay against another route fails.
        var form = new[] { ("From", "+15559998888"), ("Body", "replayed") };
        var signature = Sign(AuthToken, Url, form);

        CreateSut()
            .Verify("SMS", signature, "https://support.example.com/api/channels/email/webhook", FormBody(form))
            .Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "CC41")]
    public void CC41_AnotherProvidersRequest_IsNotAnswered()
    {
        // The composite (Task 2) relies on each verifier declining providers it does not own.
        var form = new[] { ("From", "+15559998888"), ("Body", "hello") };
        var signature = Sign(AuthToken, Url, form);

        CreateSut().Verify("WhatsApp", signature, Url, FormBody(form)).Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "CC41")]
    public void CC41_NoSmsGatewayConfigured_IsRefused()
    {
        var form = new[] { ("From", "+15559998888"), ("Body", "hello") };
        var signature = Sign(AuthToken, Url, form);

        CreateSut(secret: null).Verify("SMS", signature, Url, FormBody(form)).Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "CC41")]
    public void CC41_UrlEncodedValues_AreVerifiedDecoded()
    {
        // Twilio signs the decoded values but transmits them percent-encoded. Verifying the raw
        // encoded text would reject every message containing a space or a plus.
        var form = new[] { ("From", "+15559998888"), ("Body", "spaces & symbols") };
        var signature = Sign(AuthToken, Url, form);

        CreateSut().Verify("SMS", signature, Url, FormBody(form)).Should().BeTrue();
    }
}
```

- [x] **Step 2: Run the tests to verify they fail**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TwilioSignatureVerifierTests"
```

Expected: **build failure**, `CS0246: The type or namespace name 'TwilioSignatureVerifier' could not
be found` — the class does not exist yet.

- [x] **Step 3: Write the implementation**

Create `backend/src/CustomerSupport.Infrastructure/Channels/TwilioSignatureVerifier.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Web;
using CustomerSupport.Application.Channels;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;

namespace CustomerSupport.Infrastructure.Channels;

/// <summary>
/// Verifies Twilio's <c>X-Twilio-Signature</c> header (CC-40/CC-41): HMAC-SHA1 over the request URL
/// followed by every POST parameter's key and value concatenated in ordinal key order, Base64
/// encoded. Three things differ from <see cref="MetaSignatureVerifier"/> and all three matter —
/// SHA1 not SHA256, Base64 not hex, and URL-plus-sorted-params not the raw body. This is why
/// <see cref="IWebhookSignatureVerifier.Verify"/> carries a <c>requestUrl</c> Meta ignores.
///
/// The account auth token lives in the <c>SmsGateway</c> configuration's credential slot. As with
/// the Meta verifier, it arrives already decrypted from the database provider's boundary — a second
/// Unprotect here is the CC-51 defect, not a safety measure.
/// </summary>
public sealed class TwilioSignatureVerifier(
    IExternalApiConfigurationProvider configProvider)
    : IWebhookSignatureVerifier
{
    public bool Verify(string provider, string? signature, string? requestUrl, byte[] rawBody)
    {
        if (provider != "SMS")
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(requestUrl))
        {
            return false;
        }

        var config = configProvider.GetConfig(NotificationGatewayConstants.SmsGatewayConfigName);
        var secret = config?.Auth.Value;
        if (string.IsNullOrEmpty(secret))
        {
            return false;
        }

        var expected = Compute(secret, requestUrl, rawBody);
        var received = signature.Trim();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(received));
    }

    private static string Compute(string secret, string requestUrl, byte[] rawBody)
    {
        // Twilio signs decoded values, so the form has to be parsed rather than hashed as received.
        // An empty body is legal here: the URL alone is then the signed material.
        var form = HttpUtility.ParseQueryString(
            Encoding.UTF8.GetString(rawBody ?? []), Encoding.UTF8);

        var payload = new StringBuilder(requestUrl);
        foreach (var key in form.AllKeys.Where(k => k is not null).OrderBy(k => k, StringComparer.Ordinal))
        {
            payload.Append(key).Append(form[key]);
        }

        using var mac = new HMACSHA1(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(mac.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString())));
    }
}
```

`System.Web.HttpUtility` needs no extra package reference — plan 1's `SmsNotificationChannelSender`
already uses it in this same project.

- [x] **Step 4: Run the tests to verify they pass**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TwilioSignatureVerifierTests"
```

Expected: **PASS**, 10 tests (8 facts + the 3-case theory counts as 3, minus… count as reported;
the assertion that matters is `Failed: 0`).

- [x] **Step 5: Do not commit** — no commits this session (Global Constraints).

---

## Task 2: `CompositeWebhookSignatureVerifier` and the registration swap

**Criteria:** `CC-40`, `CC-41` (the wiring half). Spec `A22`.

**Files:**
- Create: `backend/src/CustomerSupport.Infrastructure/Channels/CompositeWebhookSignatureVerifier.cs`
- Modify: `backend/src/CustomerSupport.Infrastructure/ServiceCollectionExtensions.cs:107`
- Test: `backend/tests/CustomerSupport.Tests/Unit/Channels/CompositeWebhookSignatureVerifierTests.cs`

**Interfaces:**
- Consumes: `TwilioSignatureVerifier` (Task 1); the existing `MetaSignatureVerifier`;
  `IWebhookSignatureVerifier`.
- Produces: `CompositeWebhookSignatureVerifier(IEnumerable<IWebhookSignatureVerifier> verifiers)` —
  the only type registered for `IWebhookSignatureVerifier` after this task. Tasks 3 and every
  existing webhook controller resolve it through the interface and do not know it exists.

**The line being changed** (`ServiceCollectionExtensions.cs:107` today):

```csharp
services.AddScoped<CustomerSupport.Application.Channels.IWebhookSignatureVerifier, CustomerSupport.Infrastructure.Channels.MetaSignatureVerifier>();
```

Registering two implementations against one interface would make `IWebhookSignatureVerifier`
resolve to whichever was registered last — silently breaking WhatsApp. The composite is what makes
two providers coexist without either controller learning about the other.

- [x] **Step 1: Write the failing tests**

Create `backend/tests/CustomerSupport.Tests/Unit/Channels/CompositeWebhookSignatureVerifierTests.cs`:

```csharp
using CustomerSupport.Application.Channels;
using CustomerSupport.Infrastructure.Channels;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Channels;

/// <summary>
/// CC-40/CC-41 — two providers, one interface. Each verifier declines providers it does not own
/// (asserted in TwilioSignatureVerifierTests and by MetaSignatureVerifier's own provider gate), so
/// the composite accepts a delivery when any member accepts it.
/// </summary>
public class CompositeWebhookSignatureVerifierTests
{
    private sealed class StubVerifier(string provider, bool result) : IWebhookSignatureVerifier
    {
        public int Calls { get; private set; }

        public bool Verify(string p, string? signature, string? requestUrl, byte[] rawBody)
        {
            Calls++;
            return p == provider && result;
        }
    }

    [Fact]
    [Trait("AC", "CC40")]
    public void CC40_DelegatesToTheVerifierThatOwnsTheProvider()
    {
        var meta = new StubVerifier("WhatsApp", result: true);
        var twilio = new StubVerifier("SMS", result: true);
        var sut = new CompositeWebhookSignatureVerifier([meta, twilio]);

        sut.Verify("SMS", "sig", "https://x/y", [1]).Should().BeTrue();
        sut.Verify("WhatsApp", "sig", null, [1]).Should().BeTrue();
    }

    [Fact]
    [Trait("AC", "CC41")]
    public void CC41_WhenTheOwningVerifierRefuses_TheCompositeRefuses()
    {
        var twilio = new StubVerifier("SMS", result: false);
        var sut = new CompositeWebhookSignatureVerifier([twilio]);

        sut.Verify("SMS", "bad-sig", "https://x/y", [1]).Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "CC41")]
    public void CC41_AnUnknownProvider_IsRefused()
    {
        var sut = new CompositeWebhookSignatureVerifier(
            [new StubVerifier("WhatsApp", result: true), new StubVerifier("SMS", result: true)]);

        sut.Verify("Telegram", "sig", "https://x/y", [1]).Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "CC41")]
    public void CC41_NoVerifiersRegistered_IsRefusedRatherThanThrowing()
    {
        new CompositeWebhookSignatureVerifier([])
            .Verify("SMS", "sig", "https://x/y", [1]).Should().BeFalse();
    }
}
```

- [x] **Step 2: Run the tests to verify they fail**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~CompositeWebhookSignatureVerifierTests"
```

Expected: **build failure**, `CS0246` on `CompositeWebhookSignatureVerifier`.

- [x] **Step 3: Write the implementation**

Create `backend/src/CustomerSupport.Infrastructure/Channels/CompositeWebhookSignatureVerifier.cs`:

```csharp
using CustomerSupport.Application.Channels;

namespace CustomerSupport.Infrastructure.Channels;

/// <summary>
/// The single <see cref="IWebhookSignatureVerifier"/> the host registers once more than one provider
/// posts webhooks (CC-40/CC-41, spec A22). Each member verifier gates on the provider it owns and
/// returns false for the rest, so "any member accepts it" is the same answer as "the owning member
/// accepts it" — with no provider-name table to keep in step with the registrations.
///
/// A delivery no member owns is refused, which is the safe default: an unrecognised provider is
/// exactly the shape of an attacker probing for an unguarded webhook.
/// </summary>
public sealed class CompositeWebhookSignatureVerifier(
    IEnumerable<IWebhookSignatureVerifier> verifiers)
    : IWebhookSignatureVerifier
{
    private readonly IReadOnlyList<IWebhookSignatureVerifier> _verifiers = verifiers.ToList();

    public bool Verify(string provider, string? signature, string? requestUrl, byte[] rawBody) =>
        _verifiers.Any(v => v.Verify(provider, signature, requestUrl, rawBody));
}
```

- [x] **Step 4: Swap the registration**

In `backend/src/CustomerSupport.Infrastructure/ServiceCollectionExtensions.cs`, replace line 107:

```csharp
        // CC-40/CC-41 (spec A22) — two providers now sign webhooks, with different algorithms:
        // Meta hashes the raw body with SHA256, Twilio hashes the URL plus sorted form parameters
        // with SHA1. Both are registered as concrete types and reached only through the composite;
        // registering both against IWebhookSignatureVerifier directly would resolve to whichever
        // came last and silently break the other channel.
        services.AddScoped<CustomerSupport.Infrastructure.Channels.MetaSignatureVerifier>();
        services.AddScoped<CustomerSupport.Infrastructure.Channels.TwilioSignatureVerifier>();
        services.AddScoped<CustomerSupport.Application.Channels.IWebhookSignatureVerifier>(sp =>
            new CustomerSupport.Infrastructure.Channels.CompositeWebhookSignatureVerifier(
            [
                sp.GetRequiredService<CustomerSupport.Infrastructure.Channels.MetaSignatureVerifier>(),
                sp.GetRequiredService<CustomerSupport.Infrastructure.Channels.TwilioSignatureVerifier>(),
            ]));
```

- [x] **Step 5: Run the tests, plus every WhatsApp test, to verify nothing regressed**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~CompositeWebhookSignatureVerifierTests|FullyQualifiedName~TwilioSignatureVerifierTests|FullyQualifiedName~WhatsApp"
```

Expected: **`Failed: 0`**. The WhatsApp webhook tests are the real assertion here — they exercise
`MetaSignatureVerifier` through the newly-composited registration, so they prove the swap did not
change WhatsApp's behaviour.

- [x] **Step 6: Do not commit.**

---

## Task 3: `SmsWebhookController`

**Criteria:** `CC-40`, `CC-41` (end to end).

**Files:**
- Create: `backend/src/CustomerSupport.ExternalApi/Controllers/SmsWebhookController.cs`
- Modify: `backend/tests/CustomerSupport.Tests/Integration/GatewayTestData.cs` (add an SMS seed)
- Modify: `backend/tests/CustomerSupport.Tests/Integration/CrmExternalApiFactory.cs` (bridge method)
- Test: `backend/tests/CustomerSupport.Tests/Integration/SmsInboundWebhookTests.cs`

**Interfaces:**
- Consumes: `IMediator`; `IWebhookSignatureVerifier` (Task 2's composite, via the interface);
  `IngestInboundChannelMessageCommand(string Channel, string? CustomerName, string? CustomerPhone,
  string? CustomerEmail, string Body, string? ProviderMessageId)` — note Task 4 adds an optional
  `Subject` after this task; SMS passes nothing for it either way.
- Produces: `POST /api/channels/sms/webhook`; `GatewayTestData.SmsAuthToken` and
  `GatewayTestData.SeedSmsGatewayAsync(IServiceProvider, string baseUrl)`;
  `CrmExternalApiFactory.SeedSmsGatewayAsync(string baseUrl)`.

**The template:** `WhatsAppWebhookController.cs` (read it whole, 103 lines). Copy its exact
structure — `[ApiController]`, `[Route("api/channels/…")]`, `[ApiVersion("1.0")]`, no class-level
`[AllowAnonymous]` (the attribute sits only on the action, following the `KnowledgeBaseController`
convention), `Request.EnableBuffering()` then copy the body to a `MemoryStream` before anything
touches it, verify, and only then parse. Return `200` on any authentic delivery regardless of the
downstream outcome — a failed ingestion is not a retryable webhook and Twilio would otherwise
redeliver forever.

**One difference that matters:** Twilio signs the URL it was configured with. Reconstruct it with
`Request.GetDisplayUrl()` (from `Microsoft.AspNetCore.Http.Extensions`), which yields
scheme+host+path+query — the same string Twilio built its signature over, provided the host header
reaching the app matches what Twilio was pointed at.

- [x] **Step 1: Add the SMS gateway seed helper**

In `backend/tests/CustomerSupport.Tests/Integration/GatewayTestData.cs`, add alongside the existing
WhatsApp helper (mirroring it exactly — including the `ReloadAsync()` at the end, without which the
provider serves its cached rows and every signature check fails):

```csharp
    public const string SmsAuthToken = "twilio-auth-token-for-tests-only";

    /// <summary>
    /// Provisions the SmsGateway row TwilioSignatureVerifier reads its account auth token from
    /// (CC-40/CC-41). Unlike WhatsApp, only Auth.Value is needed: the inbound verifier reads Value,
    /// and no test here dispatches outbound SMS through this row.
    /// </summary>
    public static async Task SeedSmsGatewayAsync(IServiceProvider services, string baseUrl)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

        if (!await db.Categories.AnyAsync(c => c.Name == "General" && c.IsActive))
        {
            db.Categories.Add(Category.Create("General"));
        }

        var protectedSecret = protector.Protect(SmsAuthToken);
        var existing = await db.Set<ExternalApiConfiguration>()
            .SingleOrDefaultAsync(c => c.Name == "SmsGateway");

        if (existing is null)
        {
            db.Set<ExternalApiConfiguration>().Add(ExternalApiConfiguration.Create(
                "SmsGateway",
                baseUrl: baseUrl,
                timeoutSeconds: 30,
                authType: "Bearer",
                authValue: protectedSecret,
                authToken: protectedSecret));
        }
        else
        {
            existing.UpdateConfig(baseUrl, 30);
            existing.UpdateAuth(authType: "Bearer", authValue: protectedSecret, authToken: protectedSecret);
            db.Set<ExternalApiConfiguration>().Update(existing);
        }

        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IExternalApiConfigurationProvider>().ReloadAsync();
    }
```

In `CrmExternalApiFactory.cs`, beside the existing WhatsApp bridge at line 39:

```csharp
    /// <summary>Seeds the SmsGateway configuration TwilioSignatureVerifier reads (CC-40/CC-41).</summary>
    public Task SeedSmsGatewayAsync(string baseUrl) => GatewayTestData.SeedSmsGatewayAsync(Services, baseUrl);
```

- [x] **Step 2: Write the failing tests**

Create `backend/tests/CustomerSupport.Tests/Integration/SmsInboundWebhookTests.cs`:

```csharp
using System.Net;
using System.Security.Cryptography;
using System.Text;
using CustomerSupport.Domain.Common;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// FEAT-25 — Twilio's inbound SMS webhook against the real external host. CC-40 (a validly-signed
/// delivery runs the shared ingestion path with Channel=SMS) and CC-41 (an unsigned or wrongly
/// signed delivery is refused before any database write).
/// </summary>
public class SmsInboundWebhookTests : IAsyncLifetime
{
    private const string Path = "/api/channels/sms/webhook";
    private readonly CrmExternalApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        await _factory.SeedSmsGatewayAsync("https://api.twilio.test/2010-04-01/Accounts/ACtest/Messages.json");
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync().AsTask();
    }

    /// <summary>Twilio's recipe: URL + ordinal-sorted key/value pairs, HMAC-SHA1, Base64.</summary>
    private static string Sign(string secret, string url, params (string Key, string Value)[] form)
    {
        var payload = new StringBuilder(url);
        foreach (var (key, value) in form.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            payload.Append(key).Append(value);
        }

        using var mac = new HMACSHA1(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(mac.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString())));
    }

    private Task<HttpResponseMessage> PostAsync((string Key, string Value)[] form, string? signature)
    {
        // The URL Twilio signs is the one it posts to. CreateClient()'s BaseAddress is
        // http://localhost/, so the signed URL must be built from the same base.
        var request = new HttpRequestMessage(HttpMethod.Post, Path)
        {
            Content = new FormUrlEncodedContent(
                form.Select(f => new KeyValuePair<string, string>(f.Key, f.Value))),
        };

        if (signature is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Twilio-Signature", signature);
        }

        return _client.SendAsync(request);
    }

    private string SignedUrl => $"{_client.BaseAddress}".TrimEnd('/') + Path;

    private async Task<List<Domain.Entities.Tickets.TicketMessage>> SmsMessagesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TicketMessages.Where(m => m.Channel == ChannelNames.Sms).ToListAsync();
    }

    [Fact]
    [Trait("AC", "CC40")]
    public async Task CC40_SignedWebhook_RunsSharedIngestionAsSmsTicket()
    {
        var form = new[]
        {
            ("From", "+15551230001"),
            ("Body", "My order has not arrived"),
            ("MessageSid", "SM40000000000000000000000000000001"),
        };
        var signature = Sign(GatewayTestData.SmsAuthToken, SignedUrl, form);

        var response = await PostAsync(form, signature);

        var raw = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"body: {raw[..Math.Min(500, raw.Length)]}");

        var stored = (await SmsMessagesAsync())
            .Should().ContainSingle(m => m.ProviderMessageId == "SM40000000000000000000000000000001").Subject;
        stored.Direction.Should().Be("Inbound");
        stored.SenderId.Should().Be(SystemActors.ChannelIngestion);
        stored.Body.Should().Be("My order has not arrived");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ticket = await db.Tickets.SingleAsync(t => t.Id == stored.TicketId);
        ticket.Source.Should().Be(ChannelNames.Sms);
        var customer = await db.Customers.SingleAsync(c => c.Id == ticket.CustomerId);
        customer.Phone.Should().Be("+15551230001");
    }

    [Fact]
    [Trait("AC", "CC40")]
    public async Task CC40_RetriedDeliveryWithSameMessageSid_StoresExactlyOneMessage()
    {
        var form = new[]
        {
            ("From", "+15551230002"),
            ("Body", "Still waiting"),
            ("MessageSid", "SM40000000000000000000000000000002"),
        };
        var signature = Sign(GatewayTestData.SmsAuthToken, SignedUrl, form);

        (await PostAsync(form, signature)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PostAsync(form, signature)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await SmsMessagesAsync())
            .Where(m => m.ProviderMessageId == "SM40000000000000000000000000000002")
            .Should().HaveCount(1);
    }

    [Fact]
    [Trait("AC", "CC41")]
    public async Task CC41_UnsignedWebhook_RefusedBeforeAnyDatabaseWrite()
    {
        var form = new[]
        {
            ("From", "+15551230003"),
            ("Body", "Forged"),
            ("MessageSid", "SM40000000000000000000000000000003"),
        };

        var response = await PostAsync(form, signature: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SmsMessagesAsync())
            .Should().NotContain(m => m.ProviderMessageId == "SM40000000000000000000000000000003");
    }

    [Fact]
    [Trait("AC", "CC41")]
    public async Task CC41_WrongSignature_RefusedBeforeAnyDatabaseWrite()
    {
        var form = new[]
        {
            ("From", "+15551230004"),
            ("Body", "Forged with a bad key"),
            ("MessageSid", "SM40000000000000000000000000000004"),
        };
        var signature = Sign("the-wrong-auth-token", SignedUrl, form);

        var response = await PostAsync(form, signature);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SmsMessagesAsync())
            .Should().NotContain(m => m.ProviderMessageId == "SM40000000000000000000000000000004");
    }

    [Fact]
    [Trait("AC", "CC40")]
    public async Task CC40_SignedDeliveryWithNoBody_IsRefusedAsUningestible()
    {
        // Authentic but empty: Twilio sends delivery-status callbacks to the same URL, and they
        // carry no Body. Answering 400 (not 500) keeps them out of the ingestion path.
        var form = new[] { ("From", "+15551230005"), ("MessageSid", "SM40000000000000000000000000000005") };
        var signature = Sign(GatewayTestData.SmsAuthToken, SignedUrl, form);

        var response = await PostAsync(form, signature);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SmsMessagesAsync())
            .Should().NotContain(m => m.ProviderMessageId == "SM40000000000000000000000000000005");
    }
}
```

- [x] **Step 3: Run the tests to verify they fail**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~SmsInboundWebhookTests"
```

Expected: all six **FAIL** with `404 Not Found` (the route does not exist), except the two
`Unauthorized` cases which will also report `404` rather than `401`.

- [x] **Step 4: Write the controller**

Create `backend/src/CustomerSupport.ExternalApi/Controllers/SmsWebhookController.cs`:

```csharp
using Asp.Versioning;
using CustomerSupport.Application.Channels;
using CustomerSupport.Application.Features.Channels.Commands.IngestInboundChannelMessage;
using CustomerSupport.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.ExternalApi.Controllers;

/// <summary>
/// FEAT-25 — Twilio's inbound SMS webhook (CC-40/CC-41). Anonymous by nature: Twilio posts without
/// any bearer it trusts, so authenticity rests entirely on <c>X-Twilio-Signature</c>, checked
/// against the untouched raw body before anything else reads the stream. Structure copied from
/// <see cref="WhatsAppWebhookController"/>; the differences are the signature scheme (Twilio signs
/// the URL plus sorted form parameters) and the payload shape (form-encoded, not JSON).
/// </summary>
[ApiController]
[Route("api/channels/sms")]
[ApiVersion("1.0")]
public class SmsWebhookController(
    IMediator mediator,
    IWebhookSignatureVerifier verifier,
    ILogger<SmsWebhookController> logger)
    : ControllerBase
{
    private const string SignatureHeader = "X-Twilio-Signature";

    /// <summary>
    /// Receives an inbound SMS. Answers 200 for any authentic delivery regardless of the downstream
    /// outcome — a failed ingestion is not a retryable webhook, and Twilio would otherwise redeliver
    /// it for hours. Unsigned or mismatched deliveries are refused with 401 before any database is
    /// touched (CC-41); an authentic delivery carrying no message (a status callback to the same
    /// URL) is 400.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, ct);
        var raw = ms.ToArray();
        Request.Body.Position = 0;

        Request.Headers.TryGetValue(SignatureHeader, out var signature);

        // Twilio signs the URL it was configured to post to, so the check needs the URL as this
        // request arrived, not just the body.
        if (!verifier.Verify(ChannelNames.Sms, signature.ToString(), Request.GetDisplayUrl(), raw))
        {
            logger.LogWarning("SMS webhook refused: invalid signature (bytes: {Length})", raw.Length);
            return Unauthorized();
        }

        var form = await Request.ReadFormAsync(ct);
        var from = form["From"].ToString();
        var body = form["Body"].ToString();
        var messageSid = form["MessageSid"].ToString();

        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(body))
        {
            // Authentic but not an inbound message — Twilio posts delivery-status callbacks here too.
            logger.LogWarning("SMS webhook refused: no ingestible message (sid {Sid})", messageSid);
            return BadRequest();
        }

        await mediator.Send(new IngestInboundChannelMessageCommand(
            Channel: ChannelNames.Sms,
            CustomerName: null,
            CustomerPhone: from,
            CustomerEmail: null,
            Body: body,
            ProviderMessageId: string.IsNullOrWhiteSpace(messageSid) ? null : messageSid), ct);

        return Ok();
    }
}
```

Twilio sends no display name for the sender, so `CustomerName` is `null` and the ingestion handler's
`Customer.Create(request.CustomerName ?? phone, …)` names a new customer by their number — the same
behaviour `A5` already specified for phone-matched channels.

- [x] **Step 5: Run the tests to verify they pass**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~SmsInboundWebhookTests"
```

Expected: **`Failed: 0, Passed: 6`**.

- [x] **Step 6: Confirm WhatsApp still passes** (the shared verifier registration is now exercised
  by two channels):

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~WhatsApp"
```

Expected: **`Failed: 0`** (15 tests, per plan 1's Task 1 record).

- [x] **Step 7: Do not commit.**

---

## Task 4: optional `Subject` on the shared ingestion command

**Criteria:** enables `CC-42` and `CC-47`. Spec `A23`.

**Files:**
- Modify: `backend/src/CustomerSupport.Application/Features/Channels/Commands/IngestInboundChannelMessage/IngestInboundChannelMessageCommand.cs`
- Modify: `.../IngestInboundChannelMessageCommandHandler.cs:74`
- Modify: `.../IngestInboundChannelMessageCommandValidator.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/IngestInboundChannelMessageTests.cs` (add two)

**Interfaces:**
- Produces: `IngestInboundChannelMessageCommand(string Channel, string? CustomerName,
  string? CustomerPhone, string? CustomerEmail, string Body, string? ProviderMessageId,
  string? Subject = null)`. Tasks 5 and 7 pass `Subject`; Task 3 and the existing WhatsApp
  controller do not and must keep compiling untouched.

**Why:** the handler synthesizes `subject: $"{request.Channel} — {request.CustomerName ?? "New contact"}"`
at line 74 unconditionally. Correct for WhatsApp/SMS (no subject exists), wrong for email and the
web form, which both carry one the customer wrote. `Subject` is **last with a default** so every
existing call site is source-compatible.

- [x] **Step 1: Write the failing tests**

Append to `backend/tests/CustomerSupport.Tests/Integration/IngestInboundChannelMessageTests.cs`
(inside the existing class; it already has `SendAsync` and a `CrmApiFactory`):

```csharp
    [Fact]
    [Trait("AC", "CC42")]
    public async Task A23_ExplicitSubject_BecomesTheNewTicketsSubject()
    {
        var email = $"subject-{Guid.NewGuid():N}@example.com";

        var result = await SendAsync(new IngestInboundChannelMessageCommand(
            Channel: ChannelNames.WebForm,
            CustomerName: "Layla Haddad",
            CustomerPhone: null,
            CustomerEmail: email,
            Body: "The invoice total looks wrong.",
            ProviderMessageId: null,
            Subject: "Invoice query"));

        result.Success.Should().BeTrue();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var message = await db.TicketMessages.SingleAsync(m => m.Id == result.Data);
        var ticket = await db.Tickets.SingleAsync(t => t.Id == message.TicketId);
        ticket.Subject.Should().Be("Invoice query");
    }

    [Fact]
    [Trait("AC", "CC42")]
    public async Task A23_NoSubject_KeepsTheGeneratedDefault()
    {
        var phone = $"+1555{Random.Shared.Next(1000000, 9999999)}";

        var result = await SendAsync(new IngestInboundChannelMessageCommand(
            Channel: ChannelNames.Sms,
            CustomerName: "Omar Nasser",
            CustomerPhone: phone,
            CustomerEmail: null,
            Body: "Where is my order?",
            ProviderMessageId: null));

        result.Success.Should().BeTrue();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var message = await db.TicketMessages.SingleAsync(m => m.Id == result.Data);
        var ticket = await db.Tickets.SingleAsync(t => t.Id == message.TicketId);
        ticket.Subject.Should().Be($"{ChannelNames.Sms} — Omar Nasser");
    }

    [Fact]
    [Trait("AC", "CC42")]
    public async Task A23_SubjectOver200Characters_IsRejectedBeforeAnyWrite()
    {
        // Ticket.Create throws ArgumentException past 200 chars; the validator has to refuse it
        // first so the failure is a keyed 400 rather than an unhandled exception.
        var email = $"long-subject-{Guid.NewGuid():N}@example.com";

        var result = await SendAsync(new IngestInboundChannelMessageCommand(
            Channel: ChannelNames.WebForm,
            CustomerName: "Too Long",
            CustomerPhone: null,
            CustomerEmail: email,
            Body: "Body is fine.",
            ProviderMessageId: null,
            Subject: new string('x', 201)));

        result.Success.Should().BeFalse();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Customers.AnyAsync(c => c.Email == email)).Should().BeFalse();
    }
```

- [x] **Step 2: Run the tests to verify they fail**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~IngestInboundChannelMessageTests"
```

Expected: **build failure** — `CS1739`/`CS1503`: no `Subject` parameter on
`IngestInboundChannelMessageCommand`.

- [x] **Step 3: Add the parameter**

`IngestInboundChannelMessageCommand.cs` — append to the record, with the reasoning:

```csharp
public record IngestInboundChannelMessageCommand(
    string Channel,
    string? CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    string Body,
    string? ProviderMessageId,
    /// <summary>
    /// A23 — the subject for a newly-created ticket, when the channel actually carries one: the web
    /// form collects it and an email has a Subject: header. Null for WhatsApp and SMS, which have no
    /// subject concept, and the handler then synthesizes its "{Channel} — {Name}" default as before.
    /// Last, with a default, so existing call sites are untouched.
    /// </summary>
    string? Subject = null) : ICommand<Response<Guid>>;
```

- [x] **Step 4: Use it in the handler**

In `IngestInboundChannelMessageCommandHandler.cs`, replace the `subject:` argument at line 74:

```csharp
                // A23 — the channel's own subject when it has one (web form, email); otherwise the
                // generated default, which is all WhatsApp and SMS can offer.
                subject: string.IsNullOrWhiteSpace(request.Subject)
                    ? $"{request.Channel} — {request.CustomerName ?? "New contact"}"
                    : request.Subject.Trim(),
```

- [x] **Step 5: Add the validation rule**

In `IngestInboundChannelMessageCommandValidator.cs`, after the `CustomerName` rule:

```csharp
        // Ticket.Create throws past 200 characters (Ticket.cs:135-138). Refusing it here turns an
        // unhandled ArgumentException into a field-keyed 400. SUBJECT_MAX_LENGTH already exists and
        // already has bilingual messages, so no Resources.yaml change is needed.
        RuleFor(x => x.Subject)
            .MaximumLength(200).WithErrorCode(ApplicationErrors.Validation.SUBJECT_MAX_LENGTH)
            .When(x => x.Subject is not null);
```

- [x] **Step 6: Run the tests to verify they pass**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~IngestInboundChannelMessageTests|FullyQualifiedName~SmsInboundWebhookTests|FullyQualifiedName~WhatsAppWebhookTests"
```

Expected: the three new tests pass and every pre-existing test in these classes still passes, **with
one known exception**: `IngestInboundChannelMessageTests.CC2_MessageAfterResolution_StartsANewTicket`
is in the established 56-name pre-existing baseline (a FEAT-32 resolution-discipline failure,
unrelated). Confirm it against `/tmp/final-failed-names.txt` rather than treating it as a regression.

- [x] **Step 7: Do not commit.**

---

## Task 5: `EmailWebhookController`

**Criteria:** `CC-42`, `CC-43`. Spec `A17`, `A21`.

**Files:**
- Create: `backend/src/CustomerSupport.ExternalApi/Controllers/EmailWebhookController.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/EmailInboundWebhookTests.cs`

**Interfaces:**
- Consumes: `IMediator`; `IngestInboundChannelMessageCommand` **with** `Subject` (Task 4).
- Produces: `POST /api/channels/email/webhook`.

**SendGrid Inbound Parse's shape:** `multipart/form-data` with text fields — `headers` (the original
message's raw headers), `from` (`"Name" <addr@example.com>`), `to`, `subject`, `text`, `html`,
`envelope` (a JSON string: `{"to":["…"],"from":"…"}`). No signature (`A21`), so there is no
verification step and none is skipped by mistake.

Two parsing jobs, both done with the BCL rather than hand-rolled:
- **Sender address and display name** — `System.Net.Mail.MailAddress` parses
  `"Layla Haddad" <layla@example.com>` into `.Address` and `.DisplayName`. Falls back to the raw
  value when it will not parse, because a rejected inbound email is worse than one with an ugly name.
- **`Message-ID` for `CC-43` idempotency** — Inbound Parse has no id field of its own, but forwards
  the original headers verbatim, so the `Message-ID:` line is pulled out of `headers` with a regex.
  Absent (some senders omit it), `ProviderMessageId` is null and the shared handler simply does not
  deduplicate — the same behaviour it has for any channel that cannot supply an id.

- [x] **Step 1: Write the failing tests**

Create `backend/tests/CustomerSupport.Tests/Integration/EmailInboundWebhookTests.cs`:

```csharp
using System.Net;
using CustomerSupport.Domain.Common;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// FEAT-35 — inbound email in SendGrid Inbound Parse's shape (CC-42), and its idempotency on a
/// repeated Message-ID (CC-43). No signature: Inbound Parse does not sign its posts (spec A21).
/// </summary>
public class EmailInboundWebhookTests : IAsyncLifetime
{
    private const string Path = "/api/channels/email/webhook";
    private readonly CrmExternalApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync().AsTask();
    }

    /// <summary>Inbound Parse posts multipart/form-data with these field names.</summary>
    private Task<HttpResponseMessage> PostAsync(
        string from, string subject, string text, string? messageId, string? envelope = null)
    {
        var headers = messageId is null
            ? "Received: by mx.example.com\r\nSubject: " + subject
            : $"Received: by mx.example.com\r\nMessage-ID: {messageId}\r\nSubject: {subject}";

        var content = new MultipartFormDataContent
        {
            { new StringContent(headers), "headers" },
            { new StringContent(from), "from" },
            { new StringContent("support@example.com"), "to" },
            { new StringContent(subject), "subject" },
            { new StringContent(text), "text" },
            {
                new StringContent(envelope
                    ?? $"{{\"to\":[\"support@example.com\"],\"from\":\"{from}\"}}"),
                "envelope"
            },
        };

        return _client.PostAsync(Path, content);
    }

    private async Task<List<Domain.Entities.Tickets.TicketMessage>> EmailMessagesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TicketMessages.Where(m => m.Channel == ChannelNames.Email).ToListAsync();
    }

    [Fact]
    [Trait("AC", "CC42")]
    public async Task CC42_InboundEmail_RunsSharedIngestionAsEmailTicket()
    {
        const string messageId = "<CC42.inbound.1@mail.example.com>";

        var response = await PostAsync(
            from: "\"Layla Haddad\" <layla.cc42@example.com>",
            subject: "Refund not received",
            text: "I was told the refund would arrive last week.",
            messageId: messageId);

        var raw = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"body: {raw[..Math.Min(500, raw.Length)]}");

        var stored = (await EmailMessagesAsync())
            .Should().ContainSingle(m => m.ProviderMessageId == messageId).Subject;
        stored.Direction.Should().Be("Inbound");
        stored.SenderId.Should().Be(SystemActors.ChannelIngestion);
        stored.Body.Should().Be("I was told the refund would arrive last week.");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ticket = await db.Tickets.SingleAsync(t => t.Id == stored.TicketId);
        ticket.Source.Should().Be(ChannelNames.Email);
        // A23: the email's own Subject: header, not the synthesized "Email — Name" default.
        ticket.Subject.Should().Be("Refund not received");

        // A17: matched/created by email address, with the display name parsed out of the From header.
        var customer = await db.Customers.SingleAsync(c => c.Id == ticket.CustomerId);
        customer.Email.Should().Be("layla.cc42@example.com");
        customer.Name.Should().Be("Layla Haddad");
    }

    [Fact]
    [Trait("AC", "CC43")]
    public async Task CC43_SameMessageIdTwice_StoresExactlyOneMessage()
    {
        const string messageId = "<CC43.duplicate@mail.example.com>";

        var first = await PostAsync("dup.cc43@example.com", "Duplicate", "Sent twice", messageId);
        var second = await PostAsync("dup.cc43@example.com", "Duplicate", "Sent twice", messageId);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        (await EmailMessagesAsync())
            .Where(m => m.ProviderMessageId == messageId).Should().HaveCount(1);
    }

    [Fact]
    [Trait("AC", "CC42")]
    public async Task CC42_BarePlainAddressWithNoDisplayName_IsAccepted()
    {
        const string messageId = "<CC42.bare@mail.example.com>";

        var response = await PostAsync("bare.cc42@example.com", "No display name", "Body here", messageId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Customers.AnyAsync(c => c.Email == "bare.cc42@example.com")).Should().BeTrue();
    }

    [Fact]
    [Trait("AC", "CC42")]
    public async Task CC42_MissingMessageIdHeader_StillIngests()
    {
        var response = await PostAsync(
            "no.id.cc42@example.com", "No Message-ID", "Some senders omit it", messageId: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customer = await db.Customers.SingleAsync(c => c.Email == "no.id.cc42@example.com");
        (await db.Tickets.AnyAsync(t => t.CustomerId == customer.Id && t.Source == ChannelNames.Email))
            .Should().BeTrue();
    }

    [Fact]
    [Trait("AC", "CC42")]
    public async Task CC42_EmptyBody_IsRefusedWithoutAWrite()
    {
        var response = await PostAsync(
            "empty.cc42@example.com", "Nothing inside", text: "   ", messageId: "<CC42.empty@x>");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Customers.AnyAsync(c => c.Email == "empty.cc42@example.com")).Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "CC42")]
    public async Task CC42_UnparseableFromHeader_IsRefusedWithoutAWrite()
    {
        var response = await PostAsync(
            from: "not-an-address", subject: "Broken sender", text: "Body", messageId: "<CC42.bad@x>");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await EmailMessagesAsync()).Should().NotContain(m => m.ProviderMessageId == "<CC42.bad@x>");
    }
}
```

- [x] **Step 2: Run the tests to verify they fail**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~EmailInboundWebhookTests"
```

Expected: all six **FAIL** with `404 Not Found`.

- [x] **Step 3: Write the controller**

Create `backend/src/CustomerSupport.ExternalApi/Controllers/EmailWebhookController.cs`:

```csharp
using System.Net.Mail;
using System.Text.RegularExpressions;
using Asp.Versioning;
using CustomerSupport.Application.Features.Channels.Commands.IngestInboundChannelMessage;
using CustomerSupport.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.ExternalApi.Controllers;

/// <summary>
/// FEAT-35 — inbound email in SendGrid Inbound Parse's shape (CC-42/CC-43). Unlike the WhatsApp and
/// SMS webhooks there is **no signature to verify**: Inbound Parse does not sign its posts (spec
/// A21, and unlike SendGrid's separate Event Webhook, which does). Nothing about the sender is
/// therefore authenticated beyond what the payload itself claims — email is spoofable by design and
/// this spec does not try to solve that.
/// </summary>
[ApiController]
[Route("api/channels/email")]
[ApiVersion("1.0")]
public partial class EmailWebhookController(
    IMediator mediator,
    ILogger<EmailWebhookController> logger)
    : ControllerBase
{
    /// <summary>Inbound Parse forwards the original headers verbatim; the Message-ID line inside
    /// them is the only stable per-message id available for CC-43's idempotency.</summary>
    [GeneratedRegex(@"^Message-ID:\s*(?<id>.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex MessageIdHeader();

    /// <summary>
    /// Receives a parsed inbound email. 200 once the payload is ingestible, regardless of the
    /// downstream outcome — SendGrid retries non-2xx responses, and a message this system cannot
    /// process will not become processable on the third delivery. A payload with no usable sender or
    /// no body is 400.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);

        var rawFrom = form["from"].ToString();
        var body = form["text"].ToString();
        var subject = form["subject"].ToString();

        if (string.IsNullOrWhiteSpace(rawFrom) || string.IsNullOrWhiteSpace(body))
        {
            logger.LogWarning("Inbound email refused: missing sender or empty body");
            return BadRequest();
        }

        if (!TryParseSender(rawFrom, out var address, out var displayName))
        {
            // Deliberately not logged in full: an inbound payload is untrusted content (CC-29).
            logger.LogWarning("Inbound email refused: From header could not be parsed");
            return BadRequest();
        }

        await mediator.Send(new IngestInboundChannelMessageCommand(
            Channel: ChannelNames.Email,
            CustomerName: displayName,
            CustomerPhone: null,
            CustomerEmail: address,
            Body: body,
            ProviderMessageId: ExtractMessageId(form["headers"].ToString()),
            Subject: string.IsNullOrWhiteSpace(subject) ? null : subject), ct);

        return Ok();
    }

    /// <summary>
    /// Splits <c>"Layla Haddad" &lt;layla@example.com&gt;</c> into its address and display name.
    /// MailAddress is used rather than a regex because it already implements RFC 5322's quoting
    /// rules; a value it rejects is not an address this system can reply to, so it is refused
    /// rather than stored.
    /// </summary>
    private static bool TryParseSender(string rawFrom, out string address, out string? displayName)
    {
        try
        {
            var parsed = new MailAddress(rawFrom.Trim());
            address = parsed.Address;
            displayName = string.IsNullOrWhiteSpace(parsed.DisplayName) ? null : parsed.DisplayName;
            return true;
        }
        catch (FormatException)
        {
            address = string.Empty;
            displayName = null;
            return false;
        }
        catch (ArgumentException)
        {
            address = string.Empty;
            displayName = null;
            return false;
        }
    }

    /// <summary>Null when the sender omitted a Message-ID — the shared handler then skips
    /// deduplication, exactly as it does for any channel with no provider id.</summary>
    private static string? ExtractMessageId(string? headers)
    {
        if (string.IsNullOrWhiteSpace(headers))
        {
            return null;
        }

        var match = MessageIdHeader().Match(headers);
        return match.Success ? match.Groups["id"].Value : null;
    }
}
```

- [x] **Step 4: Run the tests to verify they pass**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~EmailInboundWebhookTests"
```

Expected: **`Failed: 0, Passed: 6`**.

- [x] **Step 5: Do not commit.**

---

## Task 6: `CC-44` — an email-sourced ticket's reply actually reaches the customer

**Criteria:** `CC-44`. Spec `A27` (which corrects this criterion's own "one-line fix" claim).

**Files:**
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/RecordTicketMessage/RecordTicketMessageCommandHandler.cs:69-96`
- Modify: `backend/tests/CustomerSupport.Tests/Integration/GatewayTestData.cs` (email gateway seed)
- Modify: `backend/tests/CustomerSupport.Tests/Integration/CrmApiFactory.cs` (bridge method)
- Test: `backend/tests/CustomerSupport.Tests/Integration/EmailOutboundReplyTests.cs`

**Interfaces:**
- Consumes: `INotificationGateway.SendAsync(NotificationDispatchRequest, ct)`;
  `NotificationChannel.Create(string)`; `StubGatewayServer.StartAsync()` /
  `.BaseUrl` / `.ReceivedBodies`.
- Produces: `GatewayTestData.EmailApiKey`,
  `GatewayTestData.SeedEmailGatewayAsync(IServiceProvider, string baseUrl)`,
  `CrmApiFactory.SeedEmailGatewayAsync(string baseUrl)`.

**What is actually wrong today.** Lines 72-89 gate on `request.Channel is "WhatsApp" or "SMS"` and
then dispatch with `PhoneNumber: phone, Email: null`. Adding `or "Email"` to that gate — which is
what this criterion originally claimed was the whole fix — would dispatch every email reply with a
null `Email` and the customer's *phone number* in `PhoneNumber`, which
`EmailNotificationChannelSender` cannot deliver. `RequestOtpCommandHandler.cs:83-92` is the
precedent for the correct shape: set `Email:` for the email channel, `PhoneNumber:` for phone
channels, never both.

**And the placeholder-address trap.** `IngestInboundChannelMessageCommandHandler.cs:115` mints
`{phone}@channel.invalid` addresses for phone-only customers, to satisfy `Customer.Email`'s
non-nullable contract without inventing a deliverable address. Dispatching an email reply to one
would be recorded as sent and delivered nowhere, so the email branch skips them and logs — the same
shape as the existing missing-phone warning.

- [x] **Step 1: Add the email gateway seed helper**

In `GatewayTestData.cs`:

```csharp
    public const string EmailApiKey = "sendgrid-api-key-for-tests-only";

    /// <summary>
    /// Provisions the EmailGateway row EmailNotificationChannelSender dispatches through (CC-44).
    /// authToken carries the credential because the sender's Bearer branch reads Auth.Token — the
    /// same Value/Token distinction CC-51 turned on.
    /// </summary>
    public static async Task SeedEmailGatewayAsync(IServiceProvider services, string baseUrl)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

        if (!await db.Categories.AnyAsync(c => c.Name == "General" && c.IsActive))
        {
            db.Categories.Add(Category.Create("General"));
        }

        var protectedSecret = protector.Protect(EmailApiKey);
        var existing = await db.Set<ExternalApiConfiguration>()
            .SingleOrDefaultAsync(c => c.Name == "EmailGateway");

        if (existing is null)
        {
            db.Set<ExternalApiConfiguration>().Add(ExternalApiConfiguration.Create(
                "EmailGateway",
                baseUrl: baseUrl,
                timeoutSeconds: 30,
                authType: "Bearer",
                authValue: protectedSecret,
                authToken: protectedSecret));
        }
        else
        {
            existing.UpdateConfig(baseUrl, 30);
            existing.UpdateAuth(authType: "Bearer", authValue: protectedSecret, authToken: protectedSecret);
            db.Set<ExternalApiConfiguration>().Update(existing);
        }

        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IExternalApiConfigurationProvider>().ReloadAsync();
    }
```

In `CrmApiFactory.cs`, beside the WhatsApp bridge at line 73:

```csharp
    /// <summary>Seeds the EmailGateway configuration the outbound email sender dispatches through (CC-44).</summary>
    public Task SeedEmailGatewayAsync(string baseUrl) => GatewayTestData.SeedEmailGatewayAsync(Services, baseUrl);
```

- [x] **Step 2: Write the failing tests**

Create `backend/tests/CustomerSupport.Tests/Integration/EmailOutboundReplyTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Common;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// CC-44 — an agent's reply on an email-sourced ticket leaves through INotificationGateway on the
/// Email channel, addressed to the customer's email. Asserted over real HTTP against
/// StubGatewayServer, the same way WhatsAppOutboundReplyTests asserts CC-10, because the defect
/// this covers (spec A27) is precisely that the wrong field reached the transport.
/// </summary>
public class EmailOutboundReplyTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private StubGatewayServer _stub = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        _stub = await StubGatewayServer.StartAsync();
        // The sender POSTs straight to config.BaseUrl; the stub's handler is mapped at /messages.
        await _factory.SeedEmailGatewayAsync($"{_stub.BaseUrl.TrimEnd('/')}/messages");
        (_client, _) = await _factory.CreateAuthenticatedClientAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _stub.DisposeAsync();
        await _factory.DisposeAsync().AsTask();
    }

    private async Task<Guid> CreateTicketAsync(string email, string? phone)
    {
        var customer = await _client.PostAsJsonAsync("/api/Customers", new
        {
            name = "Layla Haddad",
            email,
            phone,
        });
        var customerId = (await customer.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;

        var categories = await _client.GetFromJsonAsync<Response<List<CategoryRow>>>("/api/Categories");
        var categoryId = categories!.Data!.First().Id;

        var ticket = await _client.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Refund query",
            description = "Where is my refund?",
            customerId,
            categoryId,
            impact = "Medium",
            urgency = "Medium",
        });

        return (await ticket.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    private Task<HttpResponseMessage> ReplyAsync(Guid ticketId, string channel, string body) =>
        _client.PostAsJsonAsync($"/api/Tickets/{ticketId}/messages", new
        {
            direction = "Outbound",
            channel,
            body,
        });

    [Fact]
    [Trait("AC", "CC44")]
    public async Task CC44_EmailReply_DispatchesToTheEmailGatewayAddressedToTheCustomer()
    {
        var email = $"cc44-{Guid.NewGuid():N}@example.com";
        var ticketId = await CreateTicketAsync(email, phone: null);

        var response = await ReplyAsync(ticketId, ChannelNames.Email, "Your refund is on its way.");

        var raw = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, $"body: {raw[..Math.Min(500, raw.Length)]}");

        _stub.ReceivedBodies.Should().HaveCount(1, "the email reply must actually leave the process");

        // SendGrid v3's shape, as plan 1's EmailNotificationChannelSender builds it.
        using var json = JsonDocument.Parse(_stub.ReceivedBodies.Single());
        json.RootElement
            .GetProperty("personalizations")[0].GetProperty("to")[0].GetProperty("email")
            .GetString().Should().Be(email, "A27 — the customer's address, not their phone number");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.TicketMessages.SingleAsync(
            m => m.TicketId == ticketId && m.Channel == ChannelNames.Email && m.Direction == "Outbound");
        stored.Body.Should().Be("Your refund is on its way.");
    }

    [Fact]
    [Trait("AC", "CC44")]
    public async Task CC44_PlaceholderChannelInvalidAddress_IsNotDispatched()
    {
        // The address IngestInboundChannelMessageCommandHandler mints for phone-only customers.
        // A27: recording a send to it would report success and deliver nothing.
        var ticketId = await CreateTicketAsync("15551230009@channel.invalid", phone: "15551230009");

        var response = await ReplyAsync(ticketId, ChannelNames.Email, "Should not be sent.");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        _stub.ReceivedBodies.Should().BeEmpty();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.TicketMessages.SingleAsync(
            m => m.TicketId == ticketId && m.Channel == ChannelNames.Email && m.Direction == "Outbound");
        stored.Body.Should().Be("Should not be sent.", "the message is still recorded on the ticket");
    }

    [Fact]
    [Trait("AC", "CC44")]
    public async Task CC44_SystemReply_StillDoesNotCallAnyGateway()
    {
        var ticketId = await CreateTicketAsync($"cc44-system-{Guid.NewGuid():N}@example.com", phone: null);

        var response = await ReplyAsync(ticketId, ChannelNames.System, "Internal note.");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        _stub.ReceivedBodies.Should().BeEmpty();
    }

    public sealed record CategoryRow(Guid Id, string Name);
}
```

- [x] **Step 3: Run the tests to verify they fail**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~EmailOutboundReplyTests"
```

Expected: `CC44_EmailReply_DispatchesToTheEmailGatewayAddressedToTheCustomer` **FAILS** with
`_stub.ReceivedBodies` empty (`Expected collection to contain 1 item, but found 0`) — the channel
gate excludes `Email` today. The other two pass already; keep them, they are the regression fence
around this change.

- [x] **Step 4: Rewrite the dispatch block**

In `RecordTicketMessageCommandHandler.cs`, replace lines 69-96 entirely:

```csharp
        // CC-10/CC-13/CC-44 — an agent reply on a customer-facing channel leaves through the same
        // notification gateway outbound system notifications use. The contact field is per channel
        // and never both (RequestOtpCommandHandler.cs:83-92 is the precedent): phone channels carry
        // PhoneNumber, email carries Email. Dispatching email with PhoneNumber set — which adding
        // "Email" to the old channel gate alone would have done — reaches nobody (spec A27).
        if (request.Direction == "Outbound"
            && request.Channel is ChannelNames.WhatsApp or ChannelNames.Sms or ChannelNames.Email)
        {
            var customer = await customers.GetByIdAsync(ticket.CustomerId, ct);
            var isEmail = request.Channel == ChannelNames.Email;

            // A phone-only customer's email is a deterministic {phone}@channel.invalid placeholder
            // (IngestInboundChannelMessageCommandHandler.cs:115) that exists only to satisfy
            // Customer.Email's non-nullable contract. It is not deliverable.
            var email = isEmail && customer?.Email is { } candidate
                        && !candidate.EndsWith("@channel.invalid", StringComparison.OrdinalIgnoreCase)
                ? candidate
                : null;
            var phone = isEmail ? null : customer?.Phone;
            var contact = isEmail ? email : phone;

            if (!string.IsNullOrWhiteSpace(contact))
            {
                await notificationGateway.SendAsync(new NotificationDispatchRequest(
                    TemplateCode: "TICKET_REPLY",
                    RecipientUserId: null,
                    Channels: [NotificationChannel.Create(request.Channel)],
                    Variables: new Dictionary<string, string> { ["Title"] = "Ticket reply", ["Message"] = request.Body },
                    Email: email,
                    PhoneNumber: phone,
                    BypassUserSettings: true,
                    DeduplicationKey: null,
                    CorrelationId: request.TicketId.ToString()), ct);
            }
            else
            {
                logger.LogWarning(
                    "Outbound {Channel} reply {MessageId} for ticket {TicketId} had no deliverable customer contact to send to",
                    request.Channel, message.Id, request.TicketId);
            }
        }
```

Add `using CustomerSupport.Domain.Common;` if the file does not already have it (it does — line 6).

- [x] **Step 5: Run the tests to verify they pass**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~EmailOutboundReplyTests|FullyQualifiedName~WhatsAppOutboundReplyTests"
```

Expected: **`Failed: 0`** — 3 new email tests plus the 2 existing WhatsApp ones, which are the proof
the rewritten block did not change phone-channel behaviour. Note the warning message changed text
("no deliverable customer contact" replaces "no customer phone"); no test asserts on it.

- [x] **Step 6: Do not commit.**

---

## Task 7: `WebFormController`, the throttle, and the reference query

**Criteria:** `CC-47` as revised (`CC-20`–`CC-23` unchanged). Spec `A20`, `A24`, `A25`.

**Files:**
- Create: `backend/src/CustomerSupport.Application/Channels/IWebFormSubmissionThrottle.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Channels/WebFormSubmissionThrottle.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTicketReferenceForMessage/GetTicketReferenceForMessageQuery.cs`
- Create: `.../GetTicketReferenceForMessage/GetTicketReferenceForMessageQueryHandler.cs`
- Create: `backend/src/CustomerSupport.ExternalApi/Controllers/WebFormController.cs`
- Modify: `backend/src/CustomerSupport.Infrastructure/ServiceCollectionExtensions.cs` (register the throttle)
- Test: `backend/tests/CustomerSupport.Tests/Unit/Channels/WebFormSubmissionThrottleTests.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/WebFormSubmissionTests.cs`

**Interfaces:**
- Consumes: `IngestInboundChannelMessageCommand(… , Subject)` (Task 4); `IDateTimeService.UtcNow`;
  `IMessageFactory.Success<T>(T, string)`; `ApplicationErrors.Ticket.MESSAGE_RECORDED`.
- Produces: `IWebFormSubmissionThrottle.TryAcquire(string clientKey) → bool`;
  `GetTicketReferenceForMessageQuery(Guid MessageId) : IQuery<Response<string>>`;
  `POST /api/external/webform/submit`.

**The contract is already fixed by the frontend** (`A20`) — do not redesign it:
- `frontend/projects/common/src/lib/channels/web-form.api.ts` posts
  `{name, email, subject, description, honeypot?}` to `/api/external/webform/submit`.
- It types the reply as `{reference: string, success: boolean}`, and portal-app registers
  `envelopeInterceptor` (`app.config.ts:23`), which unwraps `Response<T>` to its `data`. **So the
  endpoint returns the standard envelope with `data = {reference, success}`.** Returning a bare
  `{reference, success}` would be double-unwrapped to nothing, and returning the envelope without a
  nested `success` would still work but drift from the declared TypeScript type — populate both.
- The route is literal (`[Route("api/external/webform")]` + `[HttpPost("submit")]`), like
  `PortalController`'s `[Route("api/portal")]`. There is no controller-name-based routing here.

**Why a hand-rolled throttle** (`A24`): `AddRateLimiter`'s middleware always answers
`options.RejectionStatusCode` (`429`, `WebApiServiceExtensions.cs:53`), which is exactly the signal
`CC-47` says a caller must not be able to detect. `IMemoryCache` is not registered anywhere in this
solution, so the throttle keeps its own `ConcurrentDictionary` and takes `IDateTimeService` (already
a registered singleton, `ServiceCollectionExtensions.cs:92`) so its window is testable without
sleeping.

- [x] **Step 1: Write the failing throttle tests**

Create `backend/tests/CustomerSupport.Tests/Unit/Channels/WebFormSubmissionThrottleTests.cs`:

```csharp
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Infrastructure.Channels;
using FluentAssertions;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Channels;

/// <summary>
/// CC-47 / spec A24 — the per-IP fixed window behind the web form. A unit test with a controllable
/// clock, because the alternative is a test that sleeps for the window length.
/// </summary>
public class WebFormSubmissionThrottleTests
{
    private readonly Mock<IDateTimeService> _clock = new();
    private DateTime _now = new(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc);

    private WebFormSubmissionThrottle CreateSut()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(() => _now);
        return new WebFormSubmissionThrottle(_clock.Object);
    }

    [Fact]
    [Trait("AC", "CC47")]
    public void CC47_WithinTheLimit_IsAllowed()
    {
        var sut = CreateSut();

        for (var i = 0; i < WebFormSubmissionThrottle.PermitLimit; i++)
        {
            sut.TryAcquire("10.0.0.1").Should().BeTrue($"submission {i + 1} is inside the limit");
        }
    }

    [Fact]
    [Trait("AC", "CC47")]
    public void CC47_PastTheLimitInsideTheWindow_IsRefused()
    {
        var sut = CreateSut();
        for (var i = 0; i < WebFormSubmissionThrottle.PermitLimit; i++)
        {
            sut.TryAcquire("10.0.0.2");
        }

        sut.TryAcquire("10.0.0.2").Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "CC47")]
    public void CC47_AfterTheWindowElapses_IsAllowedAgain()
    {
        var sut = CreateSut();
        for (var i = 0; i < WebFormSubmissionThrottle.PermitLimit; i++)
        {
            sut.TryAcquire("10.0.0.3");
        }

        _now = _now.Add(WebFormSubmissionThrottle.Window).AddSeconds(1);

        sut.TryAcquire("10.0.0.3").Should().BeTrue();
    }

    [Fact]
    [Trait("AC", "CC47")]
    public void CC47_OneClientsBurst_DoesNotThrottleAnother()
    {
        var sut = CreateSut();
        for (var i = 0; i < WebFormSubmissionThrottle.PermitLimit + 3; i++)
        {
            sut.TryAcquire("10.0.0.4");
        }

        sut.TryAcquire("10.0.0.5").Should().BeTrue();
    }

    [Fact]
    [Trait("AC", "CC47")]
    public void CC47_ConcurrentAcquisitions_NeverExceedTheLimit()
    {
        var sut = CreateSut();

        var granted = 0;
        Parallel.For(0, 200, _ =>
        {
            if (sut.TryAcquire("10.0.0.6"))
            {
                Interlocked.Increment(ref granted);
            }
        });

        granted.Should().Be(WebFormSubmissionThrottle.PermitLimit);
    }
}
```

- [x] **Step 2: Run them to verify they fail**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~WebFormSubmissionThrottleTests"
```

Expected: **build failure**, `CS0246` on `WebFormSubmissionThrottle`.

- [x] **Step 3: Write the port and the throttle**

Create `backend/src/CustomerSupport.Application/Channels/IWebFormSubmissionThrottle.cs`:

```csharp
namespace CustomerSupport.Application.Channels;

/// <summary>
/// Rate-limits anonymous web-form submissions per client (CC-22/CC-47). A port rather than the
/// framework's rate limiter because CC-47 requires a throttled caller to receive a response
/// indistinguishable from a successful one, and the ASP.NET middleware answers a distinguishable
/// 429 (spec A24). The caller decides what a refusal looks like; this only answers whether the
/// client has budget left.
/// </summary>
public interface IWebFormSubmissionThrottle
{
    /// <param name="clientKey">Stable identifier for the caller — the remote IP address in
    /// practice. Never a value from the payload, which an attacker chooses.</param>
    /// <returns>True when the submission is inside the window's budget, false when it is not.</returns>
    bool TryAcquire(string clientKey);
}
```

Create `backend/src/CustomerSupport.Infrastructure/Channels/WebFormSubmissionThrottle.cs`:

```csharp
using System.Collections.Concurrent;
using CustomerSupport.Application.Channels;
using CustomerSupport.Application.Interfaces;

namespace CustomerSupport.Infrastructure.Channels;

/// <summary>
/// A per-client fixed window, held in memory (CC-47, spec A24). Limits match the platform's
/// existing "login" policy (WebApiServiceExtensions.cs:63-72) — five attempts per five minutes per
/// IP — because both guard an anonymous endpoint against the same kind of abuse.
///
/// In memory, and therefore per process: two hosts behind a load balancer each keep their own
/// window. That is accepted here rather than adding a distributed cache, because the defence's
/// purpose is to blunt casual abuse of a demo-stage form, and IMemoryCache is not registered in this
/// solution either. Registered as a singleton, so the dictionary survives between requests.
/// </summary>
public sealed class WebFormSubmissionThrottle(IDateTimeService clock) : IWebFormSubmissionThrottle
{
    public const int PermitLimit = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, Counter> _counters = new();

    public bool TryAcquire(string clientKey)
    {
        var counter = _counters.GetOrAdd(clientKey, _ => new Counter());
        var now = clock.UtcNow;

        lock (counter)
        {
            if (now - counter.WindowStart >= Window)
            {
                counter.WindowStart = now;
                counter.Count = 0;
            }

            if (counter.Count >= PermitLimit)
            {
                return false;
            }

            counter.Count++;
            return true;
        }
    }

    private sealed class Counter
    {
        public DateTime WindowStart { get; set; } = DateTime.MinValue;
        public int Count { get; set; }
    }
}
```

`WindowStart = DateTime.MinValue` makes a brand-new counter's first `TryAcquire` open a fresh window
rather than inheriting one.

- [x] **Step 4: Register the throttle**

In `ServiceCollectionExtensions.cs`, beside the verifier registrations from Task 2:

```csharp
        // CC-47 / spec A24 — singleton: the window lives in the instance, so a scoped or transient
        // registration would hand every request an empty counter and throttle nothing.
        services.AddSingleton<CustomerSupport.Application.Channels.IWebFormSubmissionThrottle,
            CustomerSupport.Infrastructure.Channels.WebFormSubmissionThrottle>();
```

- [x] **Step 5: Run the throttle tests to verify they pass**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~WebFormSubmissionThrottleTests"
```

Expected: **`Failed: 0, Passed: 5`**.

- [x] **Step 6: Write the failing endpoint tests**

Create `backend/tests/CustomerSupport.Tests/Integration/WebFormSubmissionTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CustomerSupport.Domain.Common;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// CC-47 (as revised) — the portal web form's backend. The valid submission creates a ticket and
/// returns its reference; a honeypot-filled submission and a throttled burst return responses a
/// caller outside the process cannot tell apart from the valid one, while creating nothing.
/// The request/response contract is the portal's, already fixed (spec A20):
/// frontend/projects/common/src/lib/channels/web-form.api.ts.
/// </summary>
public class WebFormSubmissionTests : IAsyncLifetime
{
    private const string Path = "/api/external/webform/submit";
    private readonly CrmExternalApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync().AsTask();
    }

    private Task<HttpResponseMessage> SubmitAsync(
        string email, string subject = "Cannot sign in", string? honeypot = null) =>
        _client.PostAsJsonAsync(Path, new
        {
            name = "Layla Haddad",
            email,
            subject,
            description = "The sign-in page rejects my password.",
            honeypot,
        });

    /// <summary>The envelope's data payload — what portal-app's envelopeInterceptor unwraps to.</summary>
    private static async Task<(string Reference, bool Success)> ReadDataAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");
        return (data.GetProperty("reference").GetString()!, data.GetProperty("success").GetBoolean());
    }

    [Fact]
    [Trait("AC", "CC47")]
    public async Task CC47_ValidSubmission_CreatesAWebFormTicketAndReturnsItsReference()
    {
        var email = $"cc47-valid-{Guid.NewGuid():N}@example.com";

        var response = await SubmitAsync(email);

        var raw = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, $"body: {raw[..Math.Min(500, raw.Length)]}");

        var (reference, success) = await ReadDataAsync(response);
        success.Should().BeTrue();
        reference.Should().MatchRegex(@"^TKT-\d{6}$", "the portal shows this to the customer");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customer = await db.Customers.SingleAsync(c => c.Email == email);
        var ticket = await db.Tickets.SingleAsync(t => t.CustomerId == customer.Id);
        ticket.Reference.Should().Be(reference, "the returned reference must be the real one");
        ticket.Source.Should().Be(ChannelNames.WebForm);
        ticket.Subject.Should().Be("Cannot sign in", "A23 — the subject the customer typed");
    }

    [Fact]
    [Trait("AC", "CC47")]
    public async Task CC47_HoneypotFilled_LooksIdenticalAndCreatesNothing()
    {
        var email = $"cc47-honeypot-{Guid.NewGuid():N}@example.com";

        var response = await SubmitAsync(email, honeypot: "http://spam.example.com");

        response.StatusCode.Should().Be(HttpStatusCode.Created, "indistinguishable from a real one");
        var (reference, success) = await ReadDataAsync(response);
        success.Should().BeTrue();
        reference.Should().MatchRegex(@"^TKT-\d{6}$", "a plausible reference, backed by nothing");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Customers.AnyAsync(c => c.Email == email)).Should().BeFalse();
        (await db.Tickets.AnyAsync(t => t.Reference == reference)).Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "CC47")]
    public async Task CC47_ThrottledBurst_LooksIdenticalAndCreatesNothing()
    {
        // The throttle is a singleton keyed by remote IP; every request from this client shares one
        // window. PermitLimit submissions succeed, the next is silently refused.
        var emails = Enumerable.Range(0, 7)
            .Select(i => $"cc47-burst-{i}-{Guid.NewGuid():N}@example.com").ToArray();

        var responses = new List<HttpResponseMessage>();
        foreach (var email in emails)
        {
            responses.Add(await SubmitAsync(email));
        }

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created,
            "CC-47: a throttled caller cannot tell the defence fired");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var created = await db.Customers.CountAsync(c => emails.Contains(c.Email));
        created.Should().BeLessThan(emails.Length, "the burst past the limit must create nothing");
    }

    [Fact]
    [Trait("AC", "CC47")]
    public async Task CC47_InvalidEmail_IsAFieldKeyedBadRequest()
    {
        // Validation failure is a real 400 — that is the customer correcting their own typo, not a
        // bot being deflected, and the portal's form renders the field error.
        var response = await _client.PostAsJsonAsync(Path, new
        {
            name = "Layla Haddad",
            email = "not-an-email",
            subject = "Cannot sign in",
            description = "Body",
            honeypot = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

- [x] **Step 7: Run them to verify they fail**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~WebFormSubmissionTests"
```

Expected: all four **FAIL** with `404 Not Found`.

- [x] **Step 8: Write the reference query**

Create `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTicketReferenceForMessage/GetTicketReferenceForMessageQuery.cs`:

```csharp
using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Queries.GetTicketReferenceForMessage;

/// <summary>
/// The human-readable TKT-nnnnnn reference of the ticket a message belongs to (spec A25).
///
/// Exists because the web form has to show the customer a reference the moment they submit, while
/// IngestInboundChannelMessageCommand returns the message id — and widening that shared command's
/// response would change a contract asserted by IngestInboundChannelMessageTests and consumed by
/// three other controllers that do not need it.
/// </summary>
public record GetTicketReferenceForMessageQuery(Guid MessageId) : IQuery<Response<string>>;
```

Create `.../GetTicketReferenceForMessageQueryHandler.cs`:

```csharp
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Tickets.Queries.GetTicketReferenceForMessage;

public class GetTicketReferenceForMessageQueryHandler(
    IRepository<TicketMessage> messages,
    IRepository<Ticket> tickets,
    IMessageFactory messageFactory)
    : IQueryHandler<GetTicketReferenceForMessageQuery, Response<string>>
{
    public async Task<Response<string>> Handle(GetTicketReferenceForMessageQuery request, CancellationToken ct)
    {
        var message = await messages.FirstOrDefaultAsync(m => m.Id == request.MessageId, ct);
        if (message is null)
        {
            // Ticket.NOT_FOUND for both branches: there is no MESSAGE_NOT_FOUND code (verified
            // 2026-09-02), and adding one would need a bilingual Resources.yaml entry or
            // ContractHardeningTests.EveryErrorCode_HasABilingualMessage fails.
            return messageFactory.NotFound<string>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        var ticket = await tickets.FirstOrDefaultAsync(t => t.Id == message.TicketId, ct);
        if (ticket is null)
        {
            return messageFactory.NotFound<string>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        return messageFactory.Success(ticket.Reference, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
```

**Already verified (2026-09-02):** `ApplicationErrors.Ticket` has `MESSAGE_RECORDED` but **no**
`MESSAGE_NOT_FOUND`, so both not-found branches use `ApplicationErrors.Ticket.NOT_FOUND`. Do not add
a new code — it would need a bilingual `Resources.yaml` entry or
`ContractHardeningTests.EveryErrorCode_HasABilingualMessage` fails.

- [x] **Step 9: Write the controller**

Create `backend/src/CustomerSupport.ExternalApi/Controllers/WebFormController.cs`:

```csharp
using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Channels;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Channels.Commands.IngestInboundChannelMessage;
using CustomerSupport.Application.Features.Tickets.Queries.GetTicketReferenceForMessage;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.ExternalApi.Controllers;

/// <summary>
/// FEAT-27 — the customer portal's web form (CC-20..CC-23, CC-47 as revised). The caller is
/// portal-app's own <c>web-form</c> feature, not a simulator (spec A20), and the request/response
/// contract below is that screen's, already fixed:
/// <c>frontend/projects/common/src/lib/channels/web-form.api.ts</c>.
///
/// Anonymous by design — this is the intake surface for a visitor with no account — so the honeypot
/// and the throttle are the only defences, and CC-47 requires that neither be detectable from
/// outside: both answer exactly what a real submission answers.
/// </summary>
[ApiController]
[Route("api/external/webform")]
[ApiVersion("1.0")]
public class WebFormController(
    IMediator mediator,
    IWebFormSubmissionThrottle throttle,
    IMessageFactory messageFactory,
    ILogger<WebFormController> logger)
    : ControllerBase
{
    /// <summary>
    /// Accepts a submission. A valid one creates (or appends to) the customer's open web-form ticket
    /// and returns its real reference. A honeypot-filled or throttled one returns the same 201 with
    /// a plausible reference that belongs to nothing — a caller cannot distinguish the three
    /// (CC-47). Validation failures are genuine 400s: that is a customer fixing a typo, and the
    /// portal renders the field error.
    /// </summary>
    [HttpPost("submit")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Response<WebFormSubmissionResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<WebFormSubmissionResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit([FromBody] WebFormSubmissionRequest request, CancellationToken ct)
    {
        // CC-22 — a populated honeypot is a bot: the field is hidden from real users. Answered
        // before the throttle so a bot cannot consume a human's budget.
        if (!string.IsNullOrWhiteSpace(request.Honeypot))
        {
            logger.LogInformation("Web-form submission discarded: honeypot populated");
            return PretendAccepted();
        }

        var clientKey = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        if (!throttle.TryAcquire(clientKey))
        {
            logger.LogInformation("Web-form submission discarded: client over its window budget");
            return PretendAccepted();
        }

        var ingested = await mediator.Send(new IngestInboundChannelMessageCommand(
            Channel: ChannelNames.WebForm,
            CustomerName: request.Name,
            CustomerPhone: null,
            CustomerEmail: request.Email,
            Body: request.Description,
            ProviderMessageId: null,
            Subject: request.Subject), ct);

        if (!ingested.Success)
        {
            return this.ToActionResult(ingested);
        }

        // A25 — the message id is what the shared command returns; the customer needs the ticket's
        // reference. One extra read, through MediatR like every other controller call.
        var reference = await mediator.Send(new GetTicketReferenceForMessageQuery(ingested.Data), ct);
        if (!reference.Success)
        {
            return this.ToActionResult(reference);
        }

        return StatusCode(
            StatusCodes.Status201Created,
            messageFactory.Success(
                new WebFormSubmissionResponse(reference.Data!, true),
                ApplicationErrors.Ticket.MESSAGE_RECORDED));
    }

    /// <summary>
    /// CC-47's indistinguishability requirement. The reference matches the real generator's
    /// TKT-nnnnnn shape (TicketReferenceGenerator.cs:49) but is drawn at random and never persisted,
    /// so it consumes no sequence value and resolves to no ticket.
    /// </summary>
    private IActionResult PretendAccepted() =>
        StatusCode(
            StatusCodes.Status201Created,
            messageFactory.Success(
                new WebFormSubmissionResponse($"TKT-{Random.Shared.Next(0, 1_000_000):D6}", true),
                ApplicationErrors.Ticket.MESSAGE_RECORDED));
}

/// <summary>
/// The portal's request shape, field-for-field (spec A20). <c>Honeypot</c> is optional and must
/// stay optional: the portal only sends it when its hidden input was filled.
/// </summary>
public sealed record WebFormSubmissionRequest(
    string Name,
    string Email,
    string Subject,
    string Description,
    string? Honeypot);

/// <summary>
/// Carried inside <c>Response&lt;T&gt;.Data</c>. portal-app's envelopeInterceptor
/// (<c>app.config.ts:23</c>) unwraps the envelope to <c>data</c>, so this is exactly what
/// <c>WebFormSubmissionResponse</c> in <c>web-form.api.ts</c> receives — including the nested
/// <c>success</c>, which that interface declares.
/// </summary>
public sealed record WebFormSubmissionResponse(string Reference, bool Success);
```

- [x] **Step 10: Run the endpoint tests to verify they pass**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~WebFormSubmissionTests"
```

Expected: **`Failed: 0, Passed: 4`**.

If `CC47_InvalidEmail_IsAFieldKeyedBadRequest` fails with `201` instead of `400`, the validation
pipeline is returning a `Response` with `Success == false` and `this.ToActionResult` is mapping it —
check `ResponseExtensions.ToActionResult`'s status mapping rather than changing the test.

- [x] **Step 11: Do not commit.**

---

## Task 8: gateway inbound simulators

**Criteria:** spec `A26` — makes `CC-40`–`CC-43` demonstrable without a real provider account.

**Files:**
- Create: `cms-integration-gateway/scripts/simulate-sms-inbound.js`
- Create: `cms-integration-gateway/scripts/simulate-email-inbound.js`
- Modify: `cms-integration-gateway/package.json` (two `scripts` entries)
- Modify: `cms-integration-gateway/CLAUDE.md` (document both)

**Interfaces:**
- Consumes: `cms-integration-gateway/config.js`'s `CALLBACK_BASE_URL` and `WEBHOOK_SECRET` (both
  added by plan 1's Task 13); the two endpoints from Tasks 3 and 5.
- Produces: `npm run simulate:sms`, `npm run simulate:email`.

**Note:** these post to the **ExternalApi host** (`CALLBACK_BASE_URL`, default
`http://localhost:5095`), not to the gateway itself. The gateway is the *pretend provider* here, and
a provider calls us. They are plain Node scripts with no dependency beyond the standard library,
matching `scripts/test-response-envelope.js`.

**The SMS script must sign with Twilio's algorithm using the URL it actually posts to** — the same
recipe `TwilioSignatureVerifier` implements. If the two disagree the script gets a `401`, which is
the point of running it.

- [x] **Step 1: Write the SMS simulator**

Create `cms-integration-gateway/scripts/simulate-sms-inbound.js`:

```javascript
#!/usr/bin/env node
/**
 * Simulates Twilio delivering an inbound SMS to the backend (spec A26, proving CC-40/CC-41).
 *
 * The gateway plays the provider here, so this posts OUT to the backend's ExternalApi host at
 * CALLBACK_BASE_URL — it is not a route this server hosts.
 *
 * Usage:
 *   npm run simulate:sms
 *   npm run simulate:sms -- --from +15551230001 --body "where is my order"
 *   npm run simulate:sms -- --unsigned          # expect 401 (CC-41)
 */
const crypto = require('crypto');
const config = require('../config');

function arg(name, fallback) {
    const i = process.argv.indexOf(`--${name}`);
    return i !== -1 && process.argv[i + 1] ? process.argv[i + 1] : fallback;
}

const from = arg('from', '+15551230001');
const body = arg('body', 'Hello from the inbound SMS simulator');
const messageSid = arg('sid', `SM${crypto.randomBytes(16).toString('hex')}`);
const unsigned = process.argv.includes('--unsigned');

const url = `${config.CALLBACK_BASE_URL.replace(/\/$/, '')}/api/channels/sms/webhook`;
const form = { Body: body, From: from, MessageSid: messageSid, To: '+15550000000' };

/**
 * Twilio's scheme: the URL, then every parameter's key immediately followed by its value in
 * alphabetical key order, HMAC-SHA1 with the auth token, Base64. Must match
 * TwilioSignatureVerifier.Compute exactly — a mismatch is a 401, which is the useful signal.
 */
function sign(secret, signedUrl, params) {
    const payload = Object.keys(params)
        .sort()
        .reduce((acc, key) => acc + key + params[key], signedUrl);
    return crypto.createHmac('sha1', secret).update(payload, 'utf8').digest('base64');
}

async function main() {
    const headers = { 'Content-Type': 'application/x-www-form-urlencoded' };
    if (!unsigned) {
        headers['X-Twilio-Signature'] = sign(config.WEBHOOK_SECRET, url, form);
    }

    console.log(`POST ${url}`);
    console.log(`  From=${from} MessageSid=${messageSid} signed=${!unsigned}`);

    const response = await fetch(url, {
        method: 'POST',
        headers,
        body: new URLSearchParams(form).toString(),
    });

    console.log(`  -> ${response.status} ${response.statusText}`);
    const text = await response.text();
    if (text) {
        console.log(`  -> ${text.slice(0, 400)}`);
    }

    // 401 is the correct, expected answer for --unsigned; anything else unexpected is a failure.
    const expected = unsigned ? 401 : 200;
    if (response.status !== expected) {
        console.error(`FAIL: expected ${expected}`);
        process.exit(1);
    }
    console.log('OK');
}

main().catch((error) => {
    console.error(`FAIL: ${error.message}`);
    console.error('Is the ExternalApi host running at', config.CALLBACK_BASE_URL, '?');
    process.exit(1);
});
```

- [x] **Step 2: Write the email simulator**

Create `cms-integration-gateway/scripts/simulate-email-inbound.js`:

```javascript
#!/usr/bin/env node
/**
 * Simulates SendGrid Inbound Parse delivering an inbound email to the backend (spec A26, proving
 * CC-42/CC-43). Unsigned by design: Inbound Parse does not sign its posts (spec A21).
 *
 * Usage:
 *   npm run simulate:email
 *   npm run simulate:email -- --from "Layla <layla@example.com>" --subject "Refund"
 *   npm run simulate:email -- --twice        # same Message-ID twice, proving CC-43
 */
const crypto = require('crypto');
const config = require('../config');

function arg(name, fallback) {
    const i = process.argv.indexOf(`--${name}`);
    return i !== -1 && process.argv[i + 1] ? process.argv[i + 1] : fallback;
}

const from = arg('from', '"Layla Haddad" <layla@example.com>');
const subject = arg('subject', 'Refund not received');
const text = arg('text', 'I was told the refund would arrive last week.');
const messageId = arg('id', `<${crypto.randomUUID()}@mail.example.com>`);
const twice = process.argv.includes('--twice');

const url = `${config.CALLBACK_BASE_URL.replace(/\/$/, '')}/api/channels/email/webhook`;

/** Inbound Parse posts multipart/form-data with these field names. */
function buildForm() {
    const form = new FormData();
    form.append(
        'headers',
        [
            'Received: by mx.sendgrid.net with SMTP',
            `Message-ID: ${messageId}`,
            `From: ${from}`,
            `Subject: ${subject}`,
        ].join('\r\n'),
    );
    form.append('from', from);
    form.append('to', 'support@example.com');
    form.append('subject', subject);
    form.append('text', text);
    form.append('envelope', JSON.stringify({ to: ['support@example.com'], from }));
    form.append('charsets', JSON.stringify({ text: 'UTF-8', subject: 'UTF-8' }));
    form.append('SPF', 'pass');
    return form;
}

async function post(attempt) {
    const response = await fetch(url, { method: 'POST', body: buildForm() });
    console.log(`  attempt ${attempt} -> ${response.status} ${response.statusText}`);
    if (response.status !== 200) {
        console.error('FAIL: expected 200');
        process.exit(1);
    }
}

async function main() {
    console.log(`POST ${url}`);
    console.log(`  from=${from} Message-ID=${messageId}`);

    await post(1);
    if (twice) {
        // CC-43: the same Message-ID must not create a second TicketMessage. The response is
        // identical either way; check the database (or the ticket timeline) to see one row.
        await post(2);
        console.log('  posted twice with one Message-ID — CC-43 expects exactly one stored message');
    }

    console.log('OK');
}

main().catch((error) => {
    console.error(`FAIL: ${error.message}`);
    console.error('Is the ExternalApi host running at', config.CALLBACK_BASE_URL, '?');
    process.exit(1);
});
```

- [x] **Step 3: Add the npm scripts**

In `cms-integration-gateway/package.json`, add to `scripts` beside `test:envelope`:

```json
    "simulate:sms": "node scripts/simulate-sms-inbound.js",
    "simulate:email": "node scripts/simulate-email-inbound.js",
```

- [x] **Step 4: Run both against a live host**

Start the ExternalApi host (it needs both settings or every request 500s):

```bash
cd backend && \
ConnectionStrings__DefaultConnection='Server=(localdb)\MSSQLLocalDB;Database=CustomerSupportCrm;Trusted_Connection=True;TrustServerCertificate=True' \
Jwt__Key='local-development-key-long-enough-to-pass-validation-1234567890' \
Channels__UseMocks=true \
Channels__MockWebhookSecret='dev-only-channel-webhook-secret' \
Serilog__Using__0=Serilog.Sinks.Console Serilog__WriteTo__0__Name=Console \
dotnet run --project src/CustomerSupport.ExternalApi --no-launch-profile --urls http://localhost:5095
```

`--no-launch-profile` matters: `dotnet run` otherwise applies `launchSettings.json` over the shell's
environment variables (this cost a false "the guard does not fire" reading in plan 1's Task 13).

The SMS signature is verified against the **`SmsGateway` config's `Auth.Value`**. With
`Channels:UseMocks=true`, `MockRoutingExternalApiConfigurationProvider` supplies
`Channels:MockWebhookSecret` as that value — so `WEBHOOK_SECRET` in the gateway's `.env`/`config.js`
and `Channels__MockWebhookSecret` on the host **must be the same string**, or the SMS simulator
gets a 401. Both default to `dev-only-channel-webhook-secret`.

Then, in another shell:

```bash
cd cms-integration-gateway && npm run simulate:email -- --twice && npm run simulate:sms && npm run simulate:sms -- --unsigned
```

Expected: email `200`, `200`; SMS signed `200`; SMS unsigned `401` and the script reporting `OK`
(401 is what it expects for `--unsigned`). Paste the real output into the task record. If the signed
SMS returns `401`, the two secrets differ or the signed URL does not match the posted URL — do not
"fix" it by loosening the verifier.

- [x] **Step 5: Document both in the gateway's CLAUDE.md**

Under the "Provider-faithful channel mocks (FEAT-35)" section added by plan 1:

```markdown
### Inbound simulators (FEAT-35 plan 2)

The gateway plays the provider for inbound too: these post to the **backend**
(`CALLBACK_BASE_URL`, default `http://localhost:5095`), they are not routes this server serves.

| Command | What it sends |
|---|---|
| `npm run simulate:sms` | Twilio-shaped form post to `/api/channels/sms/webhook`, signed with `WEBHOOK_SECRET` using Twilio's HMAC-SHA1-over-URL-plus-sorted-params scheme. Expects `200`. |
| `npm run simulate:sms -- --unsigned` | The same without the signature header. Expects `401` (CC-41). |
| `npm run simulate:email` | SendGrid Inbound Parse-shaped `multipart/form-data` post to `/api/channels/email/webhook`. Unsigned by design — Inbound Parse does not sign. Expects `200`. |
| `npm run simulate:email -- --twice` | The same payload twice with one `Message-ID`, which must store exactly one message (CC-43). |

`WEBHOOK_SECRET` here and `Channels__MockWebhookSecret` on the API host must match, or the signed
SMS post is refused with `401`.
```

- [x] **Step 6: Do not commit.**

---

## Task 9: verification, documentation and the task record

**Criteria:** `CC-50` re-proved (this plan adds three anonymous endpoints and changes shared DI —
it is what could break it), plus the record every task's evidence lands in.

**Files:**
- Modify: `CLAUDE.md` (repo root, Commands table)
- Create: `docs/superpowers/plans/EPIC-03-US-201-feat-35-inbound-completion/README.md`
- Modify: `docs/superpowers/specs/EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md` (status header)
- Modify: `docs/requirements/delivery-plan.md` (the FEAT-35 row)

- [x] **Step 1: Clean build under warnings-as-errors**

```bash
cd backend && dotnet build CustomerSupport.slnx --nologo
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [x] **Step 2: Full suite, with nothing listening on 3001 or 5095**

Confirm the ports are clear first — a leftover host holds a file lock on the build output and
produces `MSB3027`/`MSB3021` errors that look like build failures but are not:

```bash
netstat -ano | grep -E ":3001|:5095" | grep LISTENING || echo "ports clear"
cd backend && dotnet test CustomerSupport.slnx --logger "trx;LogFileName=inbound-completion.trx"
```

Expected: **`Failed: 56`**, the established pre-existing baseline, with `Passed`/`Total` grown by
this plan's ~30 new tests. Then confirm the failure *names* are unchanged — a count alone cannot
distinguish "fixed one, broke one" (plan 1's Task 1 learned this the hard way):

```bash
cd backend && python3 -c "
import re
with open('tests/CustomerSupport.Tests/TestResults/inbound-completion.trx', encoding='utf-8') as f:
    content = f.read()
blocks = re.findall(r'<RunInfo[^>]*outcome=\"Error\"[^>]*>\s*<Text>(.*?)</Text>', content, re.S)
names = set()
for b in blocks:
    m = re.search(r'\]\s+([\w\.\(\): \",]+?)\s+\[FAIL\]', b)
    if m:
        names.add(m.group(1).strip())
print('\n'.join(sorted(names)))
" > /tmp/inbound-failed-names.txt
diff <(sort /tmp/final-failed-names.txt | tr -d '\r') <(sort /tmp/inbound-failed-names.txt | tr -d '\r')
```

Expected: **empty diff**. A non-empty diff is a regression this plan caused — fix it before
recording the task complete. (`/tmp/final-failed-names.txt` is plan 1's saved baseline. If it is
gone, regenerate it by the same method from that plan's `cc50_no_gateway.trx`.)

- [x] **Step 3: Add the simulator commands to the repo-root CLAUDE.md**

In the Commands table, after the two rows plan 1 added:

```markdown
| Simulate inbound SMS | `cd cms-integration-gateway && npm run simulate:sms` (needs the ExternalApi host running) |
| Simulate inbound email | `cd cms-integration-gateway && npm run simulate:email` |
```

- [x] **Step 4: Update the spec's status header**

Change the "implementation has not started" clause for the inbound amendment to name what is now
built and verified, following the header's existing style. Do not delete the superseded text — this
document's own convention is to leave a note rather than rewrite history.

- [x] **Step 5: Update the delivery plan's FEAT-35 row**

`docs/requirements/delivery-plan.md` row 22 (added in plan 1) records FEAT-35. Update its status to
reflect both plans, and record explicitly that `CC-45`/`CC-46` (live chat, abandoned session) are
**deferred by instruction** — an unrecorded missing layer is indistinguishable from a forgotten one
(`.claude/skills/sdd-workflow/SKILL.md`).

- [x] **Step 6: Write the task record**

Create `docs/superpowers/plans/EPIC-03-US-201-feat-35-inbound-completion/README.md` with, per task:
the criteria covered, the **test output actually observed** (pasted, not summarized), and every
deviation from this plan with its reason. Follow
`EPIC-03-US-201-feat-35-channel-mock-gateway/README.md`'s shape — including its habit of recording
what went wrong, which is the part that has value later.

Known items that must appear in it:
- Whether `ApplicationErrors.Ticket.MESSAGE_NOT_FOUND` existed (Task 7, step 8) and what was used.
- The observed simulator output from Task 8, step 4, including the deliberate `401`.
- The `diff` result from step 2 above, stated as a result and not as an expectation.

- [x] **Step 7: Do not commit** — the whole session is under a no-commit instruction. The record
  should say so, as plan 1's does, and name the commit that *would* be made.

---

## Self-review

**Spec coverage.** Every criterion in this plan's range maps to a task:
`CC-40` → Tasks 1, 2, 3, 8 · `CC-41` → Tasks 1, 2, 3, 8 · `CC-42` → Tasks 4, 5, 8 ·
`CC-43` → Tasks 5, 8 · `CC-44` → Task 6 · `CC-47` (revised) → Task 7 · `CC-50` re-proved → Task 9.
Assumptions: `A20` → Task 7 · `A21` → Task 5 · `A22` → Tasks 1, 2 · `A23` → Task 4 ·
`A24` → Task 7 · `A25` → Task 7 · `A26` → Task 8 · `A27` → Task 6. `CC-45`/`CC-46` are deferred by
instruction and appear in no task, recorded in Task 9 step 5 rather than silently absent.

**Type consistency.** `IngestInboundChannelMessageCommand`'s `Subject` is the seventh positional
parameter with a default in Task 4 and is passed by name (`Subject:`) in Tasks 5 and 7.
`IWebFormSubmissionThrottle.TryAcquire(string)` has the same signature in Tasks 7's port,
implementation, tests and controller. `WebFormSubmissionThrottle.PermitLimit`/`Window` are `public
const`/`static readonly` because the tests read them. `GetTicketReferenceForMessageQuery(Guid
MessageId) : IQuery<Response<string>>` matches its handler's `IQueryHandler<…, Response<string>>`.
`CompositeWebhookSignatureVerifier(IEnumerable<IWebhookSignatureVerifier>)` matches both its test's
collection-expression construction and the registration in Task 2 step 4.
`GatewayTestData.SeedSmsGatewayAsync`/`SeedEmailGatewayAsync` take `(IServiceProvider, string)` like
the existing WhatsApp helper, and each gets a one-line bridge on the factory that owns the host the
test uses — SMS on `CrmExternalApiFactory` (inbound, external host), email on `CrmApiFactory`
(outbound reply, internal host).

**Known risk, stated rather than designed around.** Task 3's signed-URL assertion depends on
`Request.GetDisplayUrl()` returning what the test signed. Under `WebApplicationFactory` the base
address is `http://localhost/`, and the test builds its signed URL from `_client.BaseAddress`, so
they agree. Behind a reverse proxy that rewrites host or scheme they would not — real Twilio traffic
would need forwarded-headers middleware. That is a deployment concern, out of this plan's scope, and
noted here so the next person does not discover it as a surprise.
