# Email Channel Integration (backend) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** A working outbound email send (agent reply → customer inbox) and inbound webhook ingestion
(customer reply → ticket message), both riding the codebase's existing `ExternalApiConfiguration`/Refit
scanner infrastructure (`IWeatherClient`/`IPlaceholderClient` already register themselves through
`ExternalApiServiceCollectionExtensions.AddExternalApiServices`), the existing `EmailMessageConsumer`
MassTransit consumer, and `ISecretProtector` for webhook secret storage — no new plumbing
(`AC-196`–`AC-206`).

**Architecture:** One new Refit client `IEmailClient` (`[ExternalApiClient("Email")]`) discovered
automatically by the scanner that already finds `IWeatherClient` — zero new DI wiring. One new
anonymous, signature-verified webhook controller on `InternalApi` (`[AllowAnonymous]`, mirroring how a
provider callback has no user identity to authenticate). The existing `EmailMessageConsumer` is *upgraded*
from its current log-only no-op to actually send via `IEmailSender`, so the bus path the codebase
already wires becomes the real outbound mechanism.

**Tech Stack:** .NET 10, Refit, `Microsoft.Extensions.Http.Resilience` (already a dependency of the
external-API pattern), MassTransit (already hosts `EmailMessageConsumer`), DataProtection
(`ISecretProtector`). No MailKit — removed in Task 1.

**Spec:** [`../../specs/EPIC-10-US-203-email-integration-design.md`](../../specs/EPIC-10-US-203-email-integration-design.md)

**Not implemented this pass.** Spec and plan written ahead of any code, per explicit instruction.

## Global Constraints

- The email provider is one `ExternalApiConfiguration` row, `Name = "Email"`. No new configuration
  table, no new admin screen — the existing `ExternalApiConfigurationsController` (`api/externalapi-configs`)
  already lets an Admin create/update it. `IExternalApiConfigurationProvider.GetConfig("Email")` resolves
  it (same call `ExternalApiServiceCollectionExtensions` uses to set the base URL).
- Every new failure code registered in `SystemCode.cs`/`SystemCodeMap.cs`/`ResponseExtensions.MapFailureStatusCode`,
  per the repeated `FEAT-16` lesson.
- `TicketMessage.Create(ticketId, direction, channel, subject, body, senderId)` (verified signature,
  `Domain/Entities/Tickets/TicketMessage.cs:27`) requires a **non-empty** `senderId`. Outbound uses
  `IUserContext.UserId`; inbound (system-originated) uses the well-known `SystemActorId` constant the
  seeders already use elsewhere. Do **not** pass `null`.
- `ITicketReferenceGenerator.NextAsync()` yields `TKT-nnnnnn` (`Domain/Interfaces/ITicketReferenceGenerator.cs`).
  The inbound regex therefore matches `\[TKT-(\d+)\]` exactly — confirmed against the port, not guessed.
- `IEmailClient` is registered by the scanner because of `[ExternalApiClient("Email")]` — no manual
  `services.AddRefitClient` call is needed, exactly like `IWeatherClient`.

---

### Task 1: `IEmailClient` + `IEmailSender` + upgrade `EmailMessageConsumer` (`AC-196`–`AC-199`)

**Files:**
- Modify: `backend/src/CustomerSupport.Infrastructure/CustomerSupport.Infrastructure.csproj` (remove `MailKit`)
- Create: `backend/src/CustomerSupport.Application/ExternalApis/Clients/IEmailClient.cs`
- Create: `backend/src/CustomerSupport.Application/Interfaces/IEmailSender.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Email/RefitEmailSender.cs`
- Modify: `backend/src/CustomerSupport.Infrastructure/Messaging/Consumers/EmailMessageConsumer.cs` (send for real)
- Modify: `ApplicationErrors.cs`, `SystemCode.cs`, `SystemCodeMap.cs`, `ResponseExtensions.cs`, `Resources.yaml`
- Test: `backend/tests/CustomerSupport.Tests/Unit/RefitEmailSenderTests.cs`

**Interfaces:**
- Produces: `IEmailClient` (Refit, `[ExternalApiClient("Email")]`, discovered by the scanner).
- Produces: `IEmailSender.SendAsync(EmailMessage, CancellationToken) : Task<EmailSendResult>` — an
  Application-layer port (matches how `FEAT-21`'s `IAiService` sits in front of `OpenRouterAiService`),
  so callers depend on an abstraction, not Refit.
- Produces: `EmailMessage(string To, string Subject, string HtmlBody)` (Application DTO).

- [ ] **Step 1: Write the failing unit test**

```csharp
// backend/tests/CustomerSupport.Tests/Unit/RefitEmailSenderTests.cs
// Mirror FEAT-21's OpenRouterAiService test for the fake-handler harness: a FakeHttpMessageHandler
// queuing responses, a Refit client built against it directly (bypassing DI), assertions on both the
// outgoing request and the returned EmailSendResult.
using System.Net;
using System.Threading.Tasks;
using CustomerSupport.Application.ExternalApis.Clients;
using CustomerSupport.Application.Interfaces;
using FluentAssertions;
using Refit;
using Xunit;

namespace CustomerSupport.Tests.Unit;

public class RefitEmailSenderTests
{
    [Fact]
    [Trait("AC", "197")]
    public async Task SendAsync_TransientFailureThenSuccess_RetriesWithBackoff()
    {
        // FakeHttpMessageHandler returns 503, 503, 200 in sequence; assert 3 requests total and final success.
    }

    [Fact]
    [Trait("AC", "198")]
    public async Task SendAsync_NonTransientFailure_DoesNotRetry()
    {
        // 400 (bad recipient) -> exactly 1 request, EmailSendResult.Success == false.
    }

    [Fact]
    [Trait("AC", "199")]
    public async Task SendAsync_NoConfiguration_ReturnsNotConfiguredWithoutCallingHttp()
    {
        // IExternalApiConfigurationProvider.GetConfig("Email") == null -> 0 HTTP calls,
        // EmailSendResult.FailureCode == "EMAIL_NOT_CONFIGURED".
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~RefitEmailSenderTests"`
Expected: FAIL — types don't exist yet.

- [ ] **Step 3: Remove MailKit**

In `CustomerSupport.Infrastructure.csproj` delete:
```xml
<PackageReference Include="MailKit" />
```
(confirm no other consumer via repo-wide grep; remove any version pin in `Directory.Packages.props`
only if unused elsewhere — an orphaned pin breaks the build differently from an unused reference.)

- [ ] **Step 4: `IEmailClient` (Refit, scanner-discovered) + `IEmailSender` (Application port)**

```csharp
// backend/src/CustomerSupport.Application/ExternalApis/Clients/IEmailClient.cs
using CustomerSupport.Application.ExternalApis;
using Refit;

namespace CustomerSupport.Application.ExternalApis.Clients;

/// <summary>Transactional-email-over-HTTP provider (SendGrid/Mailgun/Postmark-shaped) — spec A1.
/// Discovered by the same scanner that registers IWeatherClient because of [ExternalApiClient("Email")].</summary>
[ExternalApiClient("Email")]
public interface IEmailClient
{
    [Post("/v3/mail/send")]
    Task<IApiResponse> SendAsync([Body] EmailSendRequest request, CancellationToken ct = default);
}

public record EmailSendRequest(
    EmailAddress From,
    IReadOnlyList<EmailPersonalization> Personalizations,
    string Subject,
    IReadOnlyList<EmailContent> Content);

public record EmailAddress(string Email);
public record EmailPersonalization(IReadOnlyList<EmailAddress> To);
public record EmailContent(string Type, string Value);
```

```csharp
// backend/src/CustomerSupport.Application/Interfaces/IEmailSender.cs
namespace CustomerSupport.Application.Interfaces;

public record EmailMessage(string To, string Subject, string HtmlBody);

/// <summary>Application-layer port in front of IEmailClient — callers never see Refit or the
/// provider wire shape (matches IAiService in front of OpenRouterAiService, FEAT-21).</summary>
public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct);
}

public record EmailSendResult(bool Success, string? FailureCode);
```

```csharp
// backend/src/CustomerSupport.Infrastructure/Email/RefitEmailSender.cs
using CustomerSupport.Application.ExternalApis.Clients;
using CustomerSupport.Application.Interfaces;

namespace CustomerSupport.Infrastructure.Email;

public class RefitEmailSender(IEmailClient? client, string fromAddress) : IEmailSender
{
    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (client == null)
            return new EmailSendResult(false, "EMAIL_NOT_CONFIGURED");

        try
        {
            var response = await client.SendAsync(new(
                new(fromAddress),
                [new([new(message.To)])],
                message.Subject,
                [new("text/html", message.HtmlBody)]), ct);

            return response.IsSuccessStatusCode
                ? new EmailSendResult(true, null)
                : new EmailSendResult(false, "EMAIL_SEND_FAILED");
        }
        catch (Exception)
        {
            // Transient cases already exhausted their retries inside the resilience handler (Step 5)
            // before this catch is reached — this is the terminal still-failed case.
            return new EmailSendResult(false, "EMAIL_SEND_FAILED");
        }
    }
}
```

`client` is nullable-injected: register `RefitEmailSender` with a factory that resolves `IEmailClient`
only if `IExternalApiConfigurationProvider.GetConfig("Email")` is non-null (the existing
`NoOpDelegatingHandler` means an unconfigured client still *resolves* but no-ops — check config
existence explicitly so `AC-199` returns the distinct not-configured code, not a generic failure).

- [ ] **Step 5: Exact retry timing on the `"Email"` client**

`AddStandardResilienceHandler()` (used by every other client) is close but not pinned to the story's
`1s/2s/4s`. Add an optional `configureResilience` parameter to `AddExternalRefitClient<TClient>` so the
existing callers are unchanged and only `"Email"` overrides:

```csharp
// ExternalApiServiceCollectionExtensions.AddExternalRefitClient<TClient> — signature gain:
public static IServiceCollection AddExternalRefitClient<TClient>(
    this IServiceCollection services,
    string apiName,
    ILoggerFactory? loggerFactory = null,
    Action<HttpStandardResilienceOptions>? configureResilience = null)
    where TClient : class
{
    // … existing ConfigureHttpClient / auth handler …
    if (configureResilience is null)
        builder.AddStandardResilienceHandler();
    else
        builder.AddStandardResilienceHandler(configureResilience);
    return services;
}
```

For `"Email"` specifically (in `RegisterRefitClient`/`AddExternalApiServices` or a one-off registration):
```csharp
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
    options.Retry.Delay = TimeSpan.FromSeconds(1); // 1s, 2s, 4s under exponential backoff
    options.Retry.ShouldHandle = args => ValueTask.FromResult(
        args.Outcome.Result?.StatusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or >= HttpStatusCode.InternalServerError);
});
```

- [ ] **Step 6: Upgrade `EmailMessageConsumer` to send for real (per existing consumer pattern)**

The codebase already registers `EmailMessageConsumer : IConsumer<EmailMessage>` (MassTransit) and
`EmailMessage` lives in `Shared.Contracts.Messages`. Promote it from log-only to real send:

```csharp
// backend/src/CustomerSupport.Infrastructure/Messaging/Consumers/EmailMessageConsumer.cs
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Shared.Contracts.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Messaging;

public class EmailMessageConsumer(IEmailSender emailSender, ILogger<EmailMessageConsumer> logger)
    : IConsumer<EmailMessage>
{
    public async Task Consume(ConsumeContext<EmailMessage> context)
    {
        var message = context.Message;
        var result = await emailSender.SendAsync(
            new EmailMessage(message.To, message.Subject, message.HtmlBody), context.CancellationToken);
        if (!result.Success)
            logger.LogWarning("Email send failed for {To} ({Code})", message.To, result.FailureCode);
    }
}
```

This is the "existing EmailMessageConsumer pattern" the design is asked to extend — publishing an
`EmailMessage` to the bus now results in a real outbound send, and the outbound command (Task 2) can
publish to the bus rather than calling `IEmailSender` directly, keeping Application persistence-free.

- [ ] **Step 7: Register the error codes**

`ApplicationErrors.cs` — `public static class Email { NOT_CONFIGURED = "EMAIL_NOT_CONFIGURED"; SEND_FAILED = "EMAIL_SEND_FAILED"; }`.
`SystemCode.cs` — `ERR057` (503, not configured, mirrors `ERR052`'s NoOpAiService pattern), `ERR058` (502, send failed).
`SystemCodeMap.cs` + `ResponseExtensions.MapFailureStatusCode` — map both; bilingual pairs in `Resources.yaml`.

- [ ] **Step 8: Run test to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~RefitEmailSenderTests"`
Expected: PASS, 3/3.

- [ ] **Step 9: Commit**

```bash
git add backend/src/CustomerSupport.Infrastructure/CustomerSupport.Infrastructure.csproj \
        backend/src/CustomerSupport.Application/ExternalApis/Clients/IEmailClient.cs \
        backend/src/CustomerSupport.Application/Interfaces/IEmailSender.cs \
        backend/src/CustomerSupport.Infrastructure/Email/RefitEmailSender.cs \
        backend/src/CustomerSupport.Infrastructure/Messaging/Consumers/EmailMessageConsumer.cs \
        backend/src/CustomerSupport.Infrastructure/ExternalApis/ExternalApiServiceCollectionExtensions.cs \
        backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs \
        backend/src/CustomerSupport.Application/Messages/SystemCode.cs \
        backend/src/CustomerSupport.Application/Messages/SystemCodeMap.cs \
        backend/src/CustomerSupport.Api.Shared/Extensions/ResponseExtensions.cs \
        backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml \
        backend/tests/CustomerSupport.Tests/Unit/RefitEmailSenderTests.cs
git commit -m "feat(email): IEmailClient + IEmailSender over ExternalApiConfiguration; EmailMessageConsumer sends (AC-196..199)"
```

---

### Task 2: Outbound reply from a ticket (`AC-200`–`AC-202`)

**Files:**
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/SendTicketReplyEmail/` (Command + Handler + Validator)
- Modify: `backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/TicketReplyEmailEndpointTests.cs`

**Interfaces:**
- Consumes: `IEmailSender` (Task 1), `Ticket.Reference` (existing), `TicketMessage.Create` (verified
  signature at `TicketMessage.cs:27`), `IUserContext.UserId` (the acting agent).
- Produces: `SendTicketReplyEmailCommand(Guid TicketId, string Body) : ICommand<Response<Guid>>`.

- [ ] **Step 1: Write the failing test**

```csharp
// TicketReplyEmailEndpointTests.cs — needs a FakeEmailSender registered in CrmApiFactory
// (always-success, capturing LastSent for AC-201), matching how NoOpMessagePublisher is faked today.
[Fact]
[Trait("AC", "200")]
public async Task AC200_SendReply_Succeeds_RecordsOutboundMessage()
{
    var ticketId = await CreateTicketWithCustomerEmailAsync("customer@example.com");
    var response = await _client.PostAsJsonAsync($"/api/Tickets/{ticketId}/email-reply", new { body = "We fixed it." });
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var messages = await _client.GetFromJsonAsync<Response<List<MessageRow>>>($"/api/Tickets/{ticketId}/messages");
    messages!.Data.Should().Contain(m => m.Direction == "Outbound" && m.Channel == "Email");
}

[Fact]
[Trait("AC", "201")]
public async Task AC201_SendReply_SubjectContainsTicketReference()
{
    // assert FakeEmailSender.LastSent.Subject contains the ticket's Reference (e.g. "[TKT-000042] …").
}

[Fact]
[Trait("AC", "202")]
public async Task AC202_SendReply_ProviderFails_NoMessageRowCreated()
{
    // swap in a failing FakeEmailSender; response is an error and the message list is unchanged.
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketReplyEmailEndpointTests"`
Expected: FAIL — route doesn't exist.

- [ ] **Step 3: Command + handler**

```csharp
// SendTicketReplyEmailCommand.cs
public record SendTicketReplyEmailCommand(Guid TicketId, string Body) : ICommand<Response<Guid>>;
```

```csharp
// SendTicketReplyEmailCommandHandler.cs
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;
using CustomerSupport.Domain.Entities.Customers;

namespace CustomerSupport.Application.Features.Tickets.Commands.SendTicketReplyEmail;

public class SendTicketReplyEmailCommandHandler(
    IRepository<Ticket> tickets,
    IRepository<Customer> customers,
    IRepository<TicketMessage> ticketMessages,
    IEmailSender emailSender,
    IUserContext userContext,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<SendTicketReplyEmailCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(SendTicketReplyEmailCommand request, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(request.TicketId, ct);
        if (ticket == null)
            return messages.NotFound<Guid>(ApplicationErrors.Ticket.NOT_FOUND);

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct);
        if (customer == null)
            return messages.NotFound<Guid>(ApplicationErrors.Customer.NOT_FOUND);

        var result = await emailSender.SendAsync(
            new(customer.Email, $"[{ticket.Reference}] {ticket.Subject}", request.Body), ct);
        if (!result.Success)
            return messages.Fail<Guid>(result.FailureCode ?? ApplicationErrors.Email.SEND_FAILED, MessageType.ServiceUnavailable);

        // TicketMessage.Create requires a non-empty senderId — the acting agent, never the request body.
        var message = TicketMessage.Create(ticket.Id, "Outbound", "Email", ticket.Subject, request.Body, userContext.UserId);
        await ticketMessages.AddAsync(message, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(message.Id, ApplicationErrors.Ticket.MESSAGE_RECORDED);
    }
}
```

(Optionally publish `EmailMessage` to the MassTransit bus instead of calling `IEmailSender` directly —
both paths end at `RefitEmailSender`; the direct call keeps the handler's outcome synchronous and
recorded immediately, which `AC-202` needs.)

- [ ] **Step 4: Controller action**

`TicketsController`: `POST /api/Tickets/{id}/email-reply`, `[Authorize]`, body `{ body: string }`.

- [ ] **Step 5: Run test to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketReplyEmailEndpointTests"`
Expected: PASS, 3/3.

- [ ] **Step 6: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Tickets/Commands/SendTicketReplyEmail/ \
        backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs \
        backend/tests/CustomerSupport.Tests/Integration/TicketReplyEmailEndpointTests.cs
git commit -m "feat(email): outbound reply from a ticket (AC-200, AC-201, AC-202)"
```

---

### Task 3: Inbound webhook ingestion (`AC-203`–`AC-206`)

**Files:**
- Create: `backend/src/CustomerSupport.Domain/Entities/Email/EmailIngestionLog.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/EmailIngestionLogConfiguration.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Email/Commands/IngestInboundEmail/` (Command + Handler)
- Create: `backend/src/CustomerSupport.Infrastructure/Security/WebhookSignatureVerifier.cs` (uses `ISecretProtector`)
- Create: `backend/src/CustomerSupport.InternalApi/Controllers/EmailWebhookController.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/EmailWebhookEndpointTests.cs`

**Interfaces:**
- Consumes: `ITicketReferenceGenerator.NextAsync()` (`TKT-nnnnnn`), `CreateTicketCommandHandler`'s write
  path (reuse, don't re-implement), `TicketMessage.Create` (verified — system sender needs the
  well-known `SystemActorId`).
- Produces: `EmailIngestionLog(Guid Id, string ExternalMessageId, string FromAddress, string Subject,
  Guid? TicketId, string Status, string? ErrorMessage, DateTime ProcessedAt)`.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
[Trait("AC", "203")]
public async Task AC203_UnmatchedSubject_CreatesNewTicket()
{
    var response = await AnonymousWebhookPostAsync(InboundEmailPayload("new-customer@example.com", "Need help", "Can't log in.", "msg-1"));
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    // a ticket now exists whose first message body matches; a Customer was created for the unknown sender (spec A5).
}

[Fact]
[Trait("AC", "204")]
public async Task AC204_MatchedReference_AppendsToExistingTicket()
{
    var (ticketId, reference) = await CreateTicketAndGetReferenceAsync();
    await AnonymousWebhookPostAsync(InboundEmailPayload("customer@example.com", $"Re: [{reference}] your issue", "Still broken.", "msg-2"));
    var messages = await _client.GetFromJsonAsync<Response<List<MessageRow>>>($"/api/Tickets/{ticketId}/messages");
    messages!.Data.Should().Contain(m => m.Direction == "Inbound" && m.Body == "Still broken.");
}

[Fact]
[Trait("AC", "205")]
public async Task AC205_DuplicateExternalMessageId_NoDuplicateCreated()
{
    var payload = InboundEmailPayload("customer@example.com", "Help", "Body", "msg-3");
    await AnonymousWebhookPostAsync(payload);
    var second = await AnonymousWebhookPostAsync(payload);
    second.StatusCode.Should().Be(HttpStatusCode.OK); // idempotent
    // only one EmailIngestionLog row + one ticket/message for msg-3.
}

[Fact]
[Trait("AC", "206")]
public async Task AC206_MalformedPayload_LogsFailureAndReturns200()
{
    var response = await AnonymousWebhookPostAsync(InboundEmailPayload("not-an-email", "", "", "msg-4"));
    response.StatusCode.Should().Be(HttpStatusCode.OK); // never make the provider retry forever
    // an EmailIngestionLog row exists for msg-4 with Status == "Failed".
}
```

(`AnonymousWebhookPostAsync` posts to `/api/email/inbound` with the signature header set to a value
`CrmApiFactory`'s configured shared secret accepts — seed a known test secret, matching how other test
configuration is already seeded.)

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~EmailWebhookEndpointTests"`
Expected: FAIL — route doesn't exist.

- [ ] **Step 3: `EmailIngestionLog` entity + config**

```csharp
// backend/src/CustomerSupport.Domain/Entities/Email/EmailIngestionLog.cs
using CustomerSupport.Domain.Entities;

namespace CustomerSupport.Domain.Entities.Email;

public class EmailIngestionLog : BaseEntity
{
    public string ExternalMessageId { get; private set; } = string.Empty;
    public string FromAddress { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public Guid? TicketId { get; private set; }
    public string Status { get; private set; } = "Processing"; // Processing|Processed|Failed
    public string? ErrorMessage { get; private set; }
    public DateTime ProcessedAt { get; private set; }

    public static EmailIngestionLog Create(string externalMessageId, string fromAddress, string subject) => new()
    {
        Id = Guid.NewGuid(),
        ExternalMessageId = externalMessageId,
        FromAddress = fromAddress,
        Subject = subject,
        Status = "Processing",
        ProcessedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
    };

    public void MarkProcessed(Guid ticketId) { Status = "Processed"; TicketId = ticketId; MarkUpdated(); }
    public void MarkFailed(string errorMessage) { Status = "Failed"; ErrorMessage = errorMessage; MarkUpdated(); }
}
```

EF config: unique index on `ExternalMessageId` (`AC-205`'s guarantee under a race; the handler check is
an optimization).

- [ ] **Step 4: `IngestInboundEmailCommand` handler**

```csharp
public class IngestInboundEmailCommandHandler(
    IRepository<EmailIngestionLog> ingestionLog,
    IRepository<Ticket> tickets,
    IRepository<Customer> customers,
    IRepository<TicketMessage> ticketMessages,
    ITicketReferenceGenerator referenceGenerator,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<IngestInboundEmailCommand, Response<Unit>>
{
    // TKT-<digits> — confirmed against ITicketReferenceGenerator.NextAsync(), not guessed.
    private static readonly Regex ReferencePattern = new(@"\[TKT-(\d+)\]", RegexOptions.Compiled);

    // Well-known system actor for inbound (customer has no login) — same constant the seeders use.
    private static readonly Guid SystemActorId = new("00000000-0000-0000-0000-000000000001");

    public async Task<Response<Unit>> Handle(IngestInboundEmailCommand request, CancellationToken ct)
    {
        if (await ingestionLog.ExistsAsync(l => l.ExternalMessageId == request.ExternalMessageId, ct))
            return messages.Success(Unit.Value, ApplicationErrors.General.SUCCESS_OPERATION); // AC-205

        var log = EmailIngestionLog.Create(request.ExternalMessageId, request.FromAddress, request.Subject);
        await ingestionLog.AddAsync(log, ct);

        try
        {
            Ticket ticket;
            var match = ReferencePattern.Match(request.Subject);
            if (match.Success)
            {
                var reference = $"TKT-{match.Groups[1].Value}";
                ticket = (await tickets.ListAsync(t => t.Reference == reference, ct)).FirstOrDefault()
                         ?? throw new InvalidOperationException($"Referenced ticket '{reference}' not found.");
            }
            else
            {
                var customer = (await customers.ListAsync(c => c.Email == request.FromAddress, ct)).FirstOrDefault()
                               ?? await CreateCustomerForUnknownSenderAsync(request.FromAddress, ct); // spec A5
                ticket = await CreateTicketFromInboundAsync(customer, request, referenceGenerator, ct); // reuse CreateTicket logic
            }

            var ticketMessage = TicketMessage.Create(ticket.Id, "Inbound", "Email", request.Subject, request.Body, SystemActorId);
            await ticketMessages.AddAsync(ticketMessage, ct);
            log.MarkProcessed(ticket.Id);
            await unitOfWork.SaveChangesAsync(ct);
            return messages.Success(Unit.Value, ApplicationErrors.General.SUCCESS_OPERATION);
        }
        catch (Exception ex)
        {
            log.MarkFailed(ex.Message);
            await unitOfWork.SaveChangesAsync(ct); // AC-206 — still 200 to the provider
            return messages.Success(Unit.Value, ApplicationErrors.General.SUCCESS_OPERATION);
        }
    }
}
```

- [ ] **Step 5: Signature verification via `ISecretProtector`**

The shared secret lives in an `ExternalApiConfiguration` field (or a `PlatformSettings` entry); read it
through `ISecretProtector.Unprotect` (the codebase's existing secret-storage convention — see
`DataProtectionSecretProtector`). Compare with a constant-time equality, never `==` on secrets:

```csharp
// backend/src/CustomerSupport.Infrastructure/Security/WebhookSignatureVerifier.cs
using CustomerSupport.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace CustomerSupport.Infrastructure.Security;

public class WebhookSignatureVerifier(ISecretProtector secretProtector, string protectedSecret) : IWebhookSignatureVerifier
{
    public bool IsValid(HttpRequest request, object payload)
    {
        if (!request.Headers.TryGetValue("X-Email-Signature", out var provided))
            return false;
        var expected = secretProtector.Unprotect(protectedSecret); // expected signature for this payload
        return ConstantTimeEquals(expected, provided.ToString());
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var result = 0;
        for (var i = 0; i < a.Length; i++)
            result |= a[i] ^ b[i];
        return result == 0;
    }
}
```

- [ ] **Step 6: Anonymous, signature-verified controller**

```csharp
// backend/src/CustomerSupport.InternalApi/Controllers/EmailWebhookController.cs
using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Features.Email.Commands.IngestInboundEmail;
using CustomerSupport.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>Provider webhook on an otherwise-authenticated host — [AllowAnonymous] by design: a
/// provider callback has no user identity. Trust comes from verifying the provider's own signature
/// inside this action, not from ASP.NET auth. Do not "fix" this into requiring a JWT a webhook can
/// never send.</summary>
[ApiController]
[Route("api/email")]
[ApiVersion("1.0")]
[AllowAnonymous]
public class EmailWebhookController(IMediator mediator, IWebhookSignatureVerifier signatureVerifier) : ControllerBase
{
    [HttpPost("inbound")]
    public async Task<IActionResult> Inbound([FromBody] InboundEmailWebhookPayload payload, CancellationToken ct)
    {
        if (!signatureVerifier.IsValid(Request, payload))
            return Unauthorized();

        var result = await mediator.Send(new IngestInboundEmailCommand(
            payload.MessageId, payload.From, payload.Subject, payload.Body), ct);
        return this.ToActionResult(result);
    }
}

public record InboundEmailWebhookPayload(string MessageId, string From, string Subject, string Body);
```

- [ ] **Step 7: Run test to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~EmailWebhookEndpointTests"`
Expected: PASS, 4/4.

- [ ] **Step 8: Commit**

```bash
git add backend/src/CustomerSupport.Domain/Entities/Email/ \
        backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/EmailIngestionLogConfiguration.cs \
        backend/src/CustomerSupport.Application/Features/Email/Commands/IngestInboundEmail/ \
        backend/src/CustomerSupport.Infrastructure/Security/WebhookSignatureVerifier.cs \
        backend/src/CustomerSupport.InternalApi/Controllers/EmailWebhookController.cs \
        backend/tests/CustomerSupport.Tests/Integration/EmailWebhookEndpointTests.cs
git commit -m "feat(email): inbound webhook ingestion, idempotent, signature-verified (AC-203..206)"
```

---

### Task 4: Migration and full-suite gate

- [ ] **Step 1: Generate the migration**

Run: `cd backend && dotnet ef migrations add AddEmailIntegration --project src/CustomerSupport.Infrastructure --startup-project src/CustomerSupport.InternalApi`

- [ ] **Step 2: Review before it is applied**

Confirm: `EmailIngestionLog.ExternalMessageId` unique index present; no unrelated schema drift from
Task 1's MailKit removal (it touches no schema — stop and investigate if the migration shows anything
else).

- [ ] **Step 3: Build and run**

Run: `cd backend && dotnet build CustomerSupport.slnx`
Expected: Build succeeded, 0 errors, 0 new warnings.

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~Email"`
Expected: PASS — every test file from Tasks 1–3.

Run: `cd backend && dotnet test CustomerSupport.slnx`
Expected: PASS, full suite, no regressions. Paste the actual summary line.

- [ ] **Step 4: Commit**

```bash
git add backend/src/CustomerSupport.Infrastructure/Migrations/
git commit -m "feat(email): migration for EmailIngestionLog"
```

## Definition of done

`AC-196`–`AC-206` each covered by a test naming it · `dotnet build` clean, 0 new warnings ·
`dotnet test CustomerSupport.slnx` green, full output pasted into the task record · task record written
to `docs/superpowers/plans/EPIC-10-US-203-email-integration/README.md`.

**No frontend counterpart** — `US-203`/`204`/`205` are backend/system-triggered per their story
headers ("no frontend feature"). `US-205`'s reply action is called from the existing ticket detail
composer (`FEAT-14`'s `TicketMessagesComponent`), which needs one new "send as email" affordance —
small enough to fold into that component's next revision, not a separate frontend plan, and not built
this pass either way.
