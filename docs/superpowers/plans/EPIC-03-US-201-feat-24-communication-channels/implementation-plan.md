# FEAT-24..27 Communication Channels (WhatsApp, SMS conversations, Live chat, Web forms) — Backend Implementation Plan

**Spec:** `docs/superpowers/specs/EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md`
**Epic:** `EPIC-03 Communication Channels`
**Features:** `FEAT-24` WhatsApp · `FEAT-25` SMS conversations · `FEAT-26` Live chat · `FEAT-27` Web forms
**Status: NOT STARTED.** Planning artifact only — no code has been written, no migration created, no
test run. Every code block below was written against the **actual current contents** of the files it
cites (read on 2026-08-27, paths and line numbers given so the next person can re-check them before
trusting this plan blindly — files change). Treat the blocks as a concrete starting diff, not
pseudocode, and re-verify line numbers before pasting.

## Sequencing and why

```
Task 1  Shared domain + ingestion command         (CC-1..CC-5)      — everything else depends on this
Task 2  WhatsApp channel                          (CC-6..CC-10)
Task 3  SMS conversations                          (CC-11..CC-13)
Task 4  Live chat                                  (CC-14..CC-19, CC-25/26 backend seams)
Task 5  Web forms                                  (CC-20..CC-23)
Task 6  Cross-cutting security + evidence gate      (CC-27..CC-29, plus re-check CC-1..CC-26)
```

Tasks 2–5 don't depend on each other once Task 1 lands. `CC-24`/`CC-25`/`CC-26` (frontend) are not
tasked — the frontend plan is written once these backend tasks are actually implemented, per the
SDD gate.

## A gap found while grounding this plan — read before Task 1

`Customer.Email` is **non-nullable and validated** (`backend/src/CustomerSupport.Domain/Entities/Customers/Customer.cs:24`,
`:61-63` — `Validate` throws `ArgumentException` on an empty email). WhatsApp and SMS contacts are
identified by phone only; a channel-created `Customer` has no email to give it. Two real options,
decided here rather than left for whoever writes the code to improvise:

- **Chosen: synthesize a deterministic placeholder** — `"{normalizedPhone}@channel.invalid"` (e.g.
  `+15551234567@channel.invalid`). `channel.invalid` is an RFC 2606-reserved TLD, so nothing ever
  delivers there by accident, and the same phone always synthesizes the same placeholder, which
  keeps `A5`'s "match by phone" rule working the *second* time that phone contacts support even
  though the match key used for the actual lookup is `Phone`, not `Email`.
- **Rejected:** relax `Customer.Email` to nullable. Rejected because `Email` is used elsewhere
  (`Customer.Email` is the match key for `A5`'s web-form path and for every existing portal/email
  flow) and a nullable email pushes a null-check onto every one of those call sites for a case that
  only this feature produces. A synthesized value contains the change to one new helper.

This is `CustomerMatcher` in Task 1 below — every channel handler calls it instead of touching
`Customer.Create` directly.

## Contract additions (exact diffs)

### `backend/src/CustomerSupport.Domain/ValueObjects/NotificationChannel.cs`

Current file (read in full — 63 lines). The change is additive to the closed set:

```csharp
public static readonly NotificationChannel InApp = new("InApp");
public static readonly NotificationChannel Email = new("Email");
public static readonly NotificationChannel Sms = new("SMS");
public static readonly NotificationChannel Push = new("Push");
public static readonly NotificationChannel WhatsApp = new("WhatsApp");   // NEW

public static NotificationChannel Create(string? channel)
{
    ...
    return channel.Trim() switch
    {
        "InApp" => InApp,
        "Email" => Email,
        "SMS" => Sms,
        "Push" => Push,
        "WhatsApp" => WhatsApp,                                          // NEW
        _ => throw new ArgumentException($"Invalid notification channel: {channel}. Must be InApp, Email, SMS, Push, or WhatsApp.", nameof(channel))
    };
}

public bool IsWhatsApp => this == WhatsApp;                              // NEW
```

### `backend/src/CustomerSupport.Infrastructure/Sla/SystemActors.cs`

Full current file is 15 lines, one constant. Add a sibling — same pattern, same reason (`TicketHistory.Record`
and `TicketMessage.Create` both refuse `Guid.Empty`, so any system-attributed row needs a stable,
non-empty, non-real-user id):

```csharp
public static class SystemActors
{
    /// <summary>The actor recorded against an auto-escalation <c>Escalated</c> history row.</summary>
    public static readonly Guid EscalationEngine = new("E0000000-0000-0000-0000-000000000001");

    /// <summary>
    /// The actor recorded as <see cref="Domain.Entities.Tickets.TicketMessage.SenderId"/> for a
    /// message ingested from an external channel (WhatsApp, SMS, web form) with no agent involved.
    /// Not the customer — <c>TicketMessage.SenderId</c> never holds a customer identity (conversation-
    /// record spec A1) — this is the channel itself acting.
    /// </summary>
    public static readonly Guid ChannelIngestion = new("E0000000-0000-0000-0000-000000000002");
}
```

### `backend/src/CustomerSupport.Domain/Entities/Tickets/TicketMessage.cs`

**Two places actually need the widened `Channel` list**, confirmed by reading both, not assumed —
this is exactly the kind of duplication that produces a defect if only one is touched:

1. `TicketMessage.cs:17` — `private static readonly string[] AllowedChannels = ["Email", "System"];`
2. `Application/Features/Tickets/Commands/RecordTicketMessage/RecordTicketMessageCommandValidator.cs:9`
   — `private static readonly string[] AllowedChannels = ["Email", "System"];` (FluentValidation's
   own copy, checked before the domain's, per the "defence-in-depth" comment already in the
   conversation-record spec's Design section)

Both become:

```csharp
private static readonly string[] AllowedChannels = ["Email", "System", "WhatsApp", "SMS", "WebForm", "LiveChat"];
```

Add the idempotency column, following the file's existing style exactly (private setter, validated
in `Create`, `Id` left unassigned for the append-only reason already documented at line 67-69):

```csharp
public string? ProviderMessageId { get; private set; }   // NEW

public static TicketMessage Create(
    Guid ticketId, string direction, string channel, string? subject, string body, Guid senderId,
    string? providerMessageId = null)                     // NEW optional param, defaults preserve every existing call site
{
    // ...existing validation unchanged...

    return new TicketMessage
    {
        TicketId = ticketId,
        Direction = direction,
        Channel = channel,
        Subject = subject,
        Body = body.Trim(),
        SenderId = senderId,
        ProviderMessageId = providerMessageId,             // NEW
        SentAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = senderId
    };
}
```

`Infrastructure/Persistence/Configurations/TicketMessageConfiguration.cs` (read alongside the entity)
gets one addition — an index, not a column change to anything existing:

```csharp
builder.Property(m => m.ProviderMessageId).HasMaxLength(200);
builder.HasIndex(m => new { m.Channel, m.ProviderMessageId })
    .IsUnique()
    .HasFilter("[ProviderMessageId] IS NOT NULL");   // SQL Server partial unique index syntax
```

## New files

### `Application/Channels/Contracts.cs`

```csharp
namespace CustomerSupport.Application.Channels;

/// <summary>A normalized inbound message from any external channel, before it becomes a Ticket/TicketMessage.</summary>
public sealed record InboundChannelMessage(
    string Channel,                 // "WhatsApp" | "SMS" | "WebForm"
    string? CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    string Body,
    string? ProviderMessageId,
    DateTime ReceivedAt);

public interface IWebhookSignatureVerifier
{
    /// <param name="provider">"WhatsApp" or "SMS" — resolves which secret/algorithm to use.</param>
    /// <param name="rawBody">The exact bytes received, before any model binding touches them.</param>
    bool Verify(string provider, HttpRequest request, byte[] rawBody);
}
```

### `Application/Features/Channels/Commands/IngestInboundChannelMessage/IngestInboundChannelMessageCommand.cs`

Modeled directly on `CreateTicketCommandHandler`
(`Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs`, read in full
above) for the ticket-creation half, and `RecordTicketMessageCommandHandler`
(`Application/Features/Tickets/Commands/RecordTicketMessage/RecordTicketMessageCommandHandler.cs`)
for the message-append half — this command is what fuses the two paths for a channel that has no
authenticated `IUserContext.UserId` to call either with directly.

```csharp
using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Channels.Commands.IngestInboundChannelMessage;

public record IngestInboundChannelMessageCommand(
    string Channel, string? CustomerName, string? CustomerPhone, string? CustomerEmail,
    string Body, string? ProviderMessageId) : ICommand<Response<Guid>>;
```

```csharp
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;
using CustomerSupport.Infrastructure.Sla;   // SystemActors — or relocate SystemActors to Application if the
                                             // dependency direction bothers review; see note below.

namespace CustomerSupport.Application.Features.Channels.Commands.IngestInboundChannelMessage;

/// <summary>CC-1..CC-4. Resolves or creates the customer, resolves or creates the open ticket for
/// (customer, channel), and appends the inbound message. One command shared by WhatsApp, SMS and
/// web-form controllers after each parses its own provider payload shape.</summary>
public class IngestInboundChannelMessageCommandHandler(
    IRepository<Customer> customers,
    IRepository<Ticket> tickets,
    IRepository<TicketMessage> messages,
    IRepository<Category> categories,
    ITicketReferenceGenerator references,
    IUnitOfWork unitOfWork,
    IMessageFactory messageFactory)
    : ICommandHandler<IngestInboundChannelMessageCommand, Response<Guid>>
{
    private const string DefaultCategoryName = "General";   // matches CategorySeeder.Names — Infrastructure/Seeders/CategorySeeder.cs:19

    public async Task<Response<Guid>> Handle(IngestInboundChannelMessageCommand request, CancellationToken ct)
    {
        // CC-9/CC-12 idempotency: a retried webhook with the same provider message id is a no-op success,
        // not a duplicate insert and not an error.
        if (request.ProviderMessageId is not null)
        {
            var existing = await messages.FirstOrDefaultAsync(
                m => m.Channel == request.Channel && m.ProviderMessageId == request.ProviderMessageId, ct);
            if (existing is not null)
            {
                return messageFactory.Success(existing.Id, ApplicationErrors.Ticket.MESSAGE_RECORDED);
            }
        }

        var customer = await ResolveOrCreateCustomerAsync(request, ct);

        var ticket = await tickets.FirstOrDefaultAsync(
            t => t.CustomerId == customer.Id
                 && t.History.Any(h => false) // placeholder — see note below on how Channel is actually matched
            , ct);

        // NOTE (real query, not the placeholder above — Ticket has no Channel column; the match is
        // via TicketMessage): resolve the most recent non-terminal ticket that has at least one
        // TicketMessage on this Channel for this customer. This needs either (a) a join query through
        // the TicketMessage repository, or (b) adding Ticket.Source (the channel a ticket originated
        // on) as a first-class column. (b) is simpler and matches how Ticket already carries
        // DepartmentId/BranchId as "organisational grouping columns nothing populates yet" — see
        // Ticket.cs:26-32. RECOMMENDATION: add `Ticket.Source` (nullable string) in this task, set it
        // once at Ticket.Create-time from the ingesting channel, and match on
        // `t.CustomerId == customer.Id && t.Source == request.Channel && t.Status != "Resolved" && t.Status != "Closed"`.
        // This plan flags the decision rather than silently picking the more invasive join.

        Ticket? nonTerminalTicket = await tickets.FirstOrDefaultAsync(
            t => t.CustomerId == customer.Id
                 && t.Source == request.Channel
                 && t.Status != "Resolved" && t.Status != "Closed",
            ct);

        Guid ticketId;
        if (nonTerminalTicket is not null)
        {
            ticketId = nonTerminalTicket.Id;   // CC-2
        }
        else
        {
            var category = await categories.FirstOrDefaultAsync(c => c.Name == DefaultCategoryName && c.IsActive, ct)
                ?? throw new InvalidOperationException($"Default category '{DefaultCategoryName}' is not seeded.");

            var reference = await references.NextAsync(ct);
            var ticket = Ticket.Create(
                reference,
                subject: $"{request.Channel} — {request.CustomerName ?? "New contact"}",
                description: request.Body,
                customerId: customer.Id,
                categoryId: category.Id,
                priority: "Normal",
                actorId: SystemActors.ChannelIngestion);
            ticket.SetSource(request.Channel);   // new Ticket method, see Task 1 domain note

            await tickets.AddAsync(ticket, ct);
            ticketId = ticket.Id;   // CC-3
        }

        var message = TicketMessage.Create(
            ticketId, "Inbound", request.Channel, subject: null, body: request.Body,
            senderId: SystemActors.ChannelIngestion, providerMessageId: request.ProviderMessageId);   // CC-4

        await messages.AddAsync(message, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return messageFactory.Success(message.Id, ApplicationErrors.Ticket.MESSAGE_RECORDED);
    }

    private async Task<Customer> ResolveOrCreateCustomerAsync(IngestInboundChannelMessageCommand request, CancellationToken ct)
    {
        if (request.CustomerPhone is { } phone)
        {
            var byPhone = await customers.FirstOrDefaultAsync(c => c.Phone == phone, ct);
            if (byPhone is not null) return byPhone;

            var placeholderEmail = $"{phone}@channel.invalid";   // see "A gap found" note above
            var created = Customer.Create(request.CustomerName ?? phone, placeholderEmail, phone);
            await customers.AddAsync(created, ct);
            return created;
        }

        if (request.CustomerEmail is { } email)
        {
            var byEmail = await customers.FirstOrDefaultAsync(c => c.Email == email.Trim().ToLowerInvariant(), ct);
            if (byEmail is not null) return byEmail;

            var created = Customer.Create(request.CustomerName ?? email, email, phone: null);
            await customers.AddAsync(created, ct);
            return created;
        }

        throw new ArgumentException("An inbound channel message needs a phone or an email to match/create a customer.");
    }
}
```

**Two decisions flagged inline above, both real and both need a call before this task is coded:**

1. **`Ticket.Source`** does not exist today (confirmed — `Ticket.cs` has no such column; only
   `DepartmentId`/`BranchId` are the "grouping, unset by every path" pattern). Adding it is a small,
   consistent extension: nullable `string`, set once at creation, following the exact precedent set
   by `DepartmentId`. Without it, "one open ticket per (customer, channel)" (`A6`) cannot be queried
   without a join through `TicketMessage`, which is slower and more fragile (it would match on *any*
   channel a ticket has ever had a message on, not the channel it started on). **Recommendation:
   add `Ticket.Source`.**
2. **`SystemActors` currently lives in `CustomerSupport.Infrastructure.Sla`**, and this handler is in
   `Application`. `Application` must not reference `Infrastructure` (the dependency rule this
   project's `CLAUDE.md` calls "the one invariant that must not bend"). **This plan's Task 1 must
   relocate `SystemActors` to `Domain` or `Application`** (it is just two `Guid` constants — nothing
   about it needs `Infrastructure`) rather than have this new handler violate the dependency rule to
   reach it. Flagging this now because it is exactly the kind of defect that is cheap to avoid before
   the file exists and expensive to unwind after three features depend on the wrong location.

### `Application/Features/Channels/Commands/IngestInboundChannelMessage/IngestInboundChannelMessageCommandValidator.cs`

Same shape as `RecordTicketMessageCommandValidator.cs` (read in full above):

```csharp
using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Channels.Commands.IngestInboundChannelMessage;

public class IngestInboundChannelMessageCommandValidator : AbstractValidator<IngestInboundChannelMessageCommand>
{
    private static readonly string[] AllowedChannels = ["WhatsApp", "SMS", "WebForm"];

    public IngestInboundChannelMessageCommandValidator()
    {
        RuleFor(x => x.Channel).Must(c => AllowedChannels.Contains(c))
            .WithErrorCode(ApplicationErrors.Validation.MESSAGE_CHANNEL_INVALID);

        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000)
            .WithErrorCode(ApplicationErrors.Validation.MESSAGE_BODY_REQUIRED);

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.CustomerPhone) || !string.IsNullOrWhiteSpace(x.CustomerEmail))
            .WithMessage("An inbound message needs a phone or an email.")
            .WithErrorCode(ApplicationErrors.Validation.CHANNEL_CONTACT_REQUIRED);   // new code, see below
    }
}
```

### New error codes — `ApplicationErrors.cs` and `Resources.yaml`

Following the exact existing pattern (`ApplicationErrors.Ticket.MESSAGE_RECORDED = "TICKET_MESSAGE_RECORDED"`
at `ApplicationErrors.cs:216`, paired with the `TICKET_MESSAGE_RECORDED:` block at
`Resources.yaml:615-617`):

```csharp
public static class Validation
{
    // ...existing MESSAGE_* codes unchanged...
    public const string CHANNEL_CONTACT_REQUIRED = "CHANNEL_CONTACT_REQUIRED";   // NEW
}

public static class Channel   // NEW nested class, same pattern as Notification/Ticket
{
    public const string WEBHOOK_SIGNATURE_INVALID = "CHANNEL_WEBHOOK_SIGNATURE_INVALID";
    public const string PAYLOAD_INVALID = "CHANNEL_PAYLOAD_INVALID";
}
```

```yaml
CHANNEL_CONTACT_REQUIRED:
  ar: "رسالة القناة الواردة تتطلب رقم هاتف أو بريدًا إلكترونيًا"
  en: "An inbound channel message needs a phone number or an email address"

CHANNEL_WEBHOOK_SIGNATURE_INVALID:
  ar: "توقيع الويب هوك غير صالح"
  en: "The webhook signature is not valid"

CHANNEL_PAYLOAD_INVALID:
  ar: "بيانات الويب هوك غير صالحة"
  en: "The webhook payload is not valid"
```

Every one of these three needs both `ar` and `en` or `EveryErrorCode_HasABilingualMessage` fails the
build — this is enforced mechanically (confirmed by the conversation-record spec's Design section
citing the same test), not by review discipline.

## Tasks

### Task 1 — Shared domain, ingestion command, and migrations

**Covers:** `CC-1`..`CC-5`.

**Files (all shown above as real diffs, not just named):**
`Domain/Entities/Tickets/TicketMessage.cs`, `Domain/Entities/Tickets/Ticket.cs` (add `Source` +
`SetSource`, see decision 1 above), `Domain/ValueObjects/NotificationChannel.cs`,
`Domain/Common/SystemActors.cs` (relocated — see decision 2 above; delete the old
`Infrastructure/Sla/SystemActors.cs` and update `SlaBreachScanner.cs`'s one call site),
`Infrastructure/Persistence/Configurations/TicketMessageConfiguration.cs`,
`Application/Channels/Contracts.cs`, `Application/Features/Channels/Commands/IngestInboundChannelMessage/*`,
`Application/Errors/ApplicationErrors.cs`, `Api.Shared/Localization/Resources.yaml`, one migration.

**Steps:**

1. Apply the `NotificationChannel`, `SystemActors` (relocated to `Domain/Common/SystemActors.cs`),
   and `TicketMessage` diffs above exactly.
2. Add `Ticket.Source` (nullable `string`, private setter) and `Ticket.SetSource(string)` — one line
   each, following `Ticket.cs`'s existing style for `DepartmentId`/`BranchId`. Call it only from the
   new ingestion handler; every existing `Ticket.Create` call site is unaffected (default `null`).
3. Implement `IngestInboundChannelMessageCommandHandler`/`Validator` exactly as drafted above.
4. Add the error codes and their bilingual pairs.
5. Add the migration: `TicketMessages.ProviderMessageId` + partial unique index, `Tickets.Source`
   (nullable, no default, no backfill needed — existing rows get `null`, which is a legitimate "not
   from a tracked channel" value, not a data-migration problem).
   `dotnet ef migrations add AddChannelIngestionSupport --project backend/src/CustomerSupport.Infrastructure --startup-project backend/src/CustomerSupport.InternalApi`
   (command form confirmed against this project's own `CLAUDE.md` command table).

**Run (when implemented):** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~IngestInboundChannelMessage"`
**Expected:** A new customer and ticket are created on first contact; a second message from the same
customer/channel appends to the existing non-terminal ticket; a duplicate `ProviderMessageId` is a
no-op; an unrecognized `Channel` or empty `Body` is rejected before any write.
**Commit (when implemented):** `feat: add shared inbound channel ingestion and widen TicketMessage channels`

### Task 2 — WhatsApp channel

**Covers:** `CC-6`..`CC-10`.

**`WhatsAppNotificationChannelSender.cs`** is `EmailNotificationChannelSender.cs` (read in full
above, 113 lines) with the payload shape and config name swapped — same constructor, same retry
loop, same `ApplyAuth`, same transient-classification helpers, copied verbatim below only where they
differ:

```csharp
namespace CustomerSupport.Infrastructure.Notifications;

public sealed class WhatsAppNotificationChannelSender : INotificationChannelSender
{
    // constructor identical to EmailNotificationChannelSender's — same four dependencies.

    public NotificationChannel SupportedChannel => NotificationChannel.WhatsApp;

    public async Task<ChannelSendResult> SendAsync(RenderedNotification notification, CancellationToken ct = default)
    {
        var config = _configProvider.GetConfig(NotificationGatewayConstants.WhatsAppGatewayConfigName);   // NEW constant
        if (config is null)
        {
            _logger.LogWarning("WhatsApp gateway configuration '{Config}' is missing", NotificationGatewayConstants.WhatsAppGatewayConfigName);
            return new ChannelSendResult(NotificationChannel.WhatsApp, false, ApplicationErrors.Notification.CONFIG_MISSING);
        }

        // WhatsApp Cloud API shape (Meta Graph API `POST /{phone-number-id}/messages`):
        var payload = new
        {
            messaging_product = "whatsapp",
            to = notification.PhoneNumber,
            type = "text",
            text = new { body = notification.Message }
        };
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, config.TimeoutSeconds));
        ApplyAuth(client, config.Auth);   // identical private method, copied as-is from EmailNotificationChannelSender

        // Retry loop, IsTransient(HttpStatusCode), IsTransient(Exception): copied verbatim from
        // EmailNotificationChannelSender.cs:55-113, swapping every `NotificationChannel.Email` for
        // `NotificationChannel.WhatsApp`. Not reproduced a second time here — same code, same file
        // shape, see that file directly when implementing.
    }
}
```

`NotificationGatewayConstants.cs` (read in full above, 27 lines) gets one new constant next to
`SmsGatewayConfigName`:

```csharp
public const string WhatsAppGatewayConfigName = "WhatsAppGateway";
```

DI registration — **`Infrastructure/ServiceCollectionExtensions.cs:66-70`** (read exactly, this is
the real current block):

```csharp
services.AddScoped<CustomerSupport.Application.Notifications.INotificationChannelSender, CustomerSupport.Infrastructure.Notifications.EmailNotificationChannelSender>();
services.AddScoped<CustomerSupport.Application.Notifications.INotificationChannelSender, CustomerSupport.Infrastructure.Notifications.SmsNotificationChannelSender>();
services.AddScoped<CustomerSupport.Application.Notifications.INotificationChannelSender, CustomerSupport.Infrastructure.Notifications.InAppNotificationChannelSender>();
services.AddScoped<CustomerSupport.Application.Notifications.INotificationChannelSender, CustomerSupport.Infrastructure.Notifications.WhatsAppNotificationChannelSender>();  // ADD this line
services.AddScoped<CustomerSupport.Application.Notifications.INotificationDispatcher, CustomerSupport.Infrastructure.Notifications.NotificationDispatcher>();
services.AddScoped<CustomerSupport.Application.Notifications.INotificationGateway, CustomerSupport.Infrastructure.Notifications.NotificationGateway>();
```

`NotificationDispatcher` (read in full above — 27 lines, `IEnumerable<INotificationChannelSender>`
injected and indexed by `SupportedChannel`) needs **no change at all**: registering one more
implementation of `INotificationChannelSender` is the entire extension point, exactly as `NG-9`'s
design intended.

**Inbound webhook.** `Infrastructure/Channels/MetaSignatureVerifier.cs`:

```csharp
namespace CustomerSupport.Infrastructure.Channels;

/// <summary>Verifies Meta's X-Hub-Signature-256 header (HMAC-SHA256 over the exact raw body, keyed
/// by the WhatsApp app secret). Must run against the untouched byte stream — model binding may
/// reformat whitespace, which would break the signature.</summary>
public sealed class MetaSignatureVerifier(IExternalApiConfigurationProvider configProvider, ISecretProtector secretProtector)
    : IWebhookSignatureVerifier
{
    public bool Verify(string provider, HttpRequest request, byte[] rawBody)
    {
        if (provider != "WhatsApp") return false;

        var config = configProvider.GetConfig(NotificationGatewayConstants.WhatsAppGatewayConfigName);
        if (config is null) return false;

        if (!request.Headers.TryGetValue("X-Hub-Signature-256", out var header)) return false;

        var secret = secretProtector.Unprotect(config.Auth.Value);  // app secret stored the same protected way as every other credential
        var expected = "sha256=" + Convert.ToHexString(
            new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret)).ComputeHash(rawBody)).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(header.ToString()));
    }
}
```

`ExternalApi/Controllers/WhatsAppWebhookController.cs` — anonymous, follows
`KnowledgeBaseController.cs`'s "no class-level `[AllowAnonymous]`, per-action instead" convention
(read and cited above, `KnowledgeBaseController.cs:30-35`):

```csharp
[ApiController]
[Route("api/channels/whatsapp")]
[ApiVersion("1.0")]
public class WhatsAppWebhookController(IMediator mediator, IWebhookSignatureVerifier verifier) : ControllerBase
{
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, ct);
        var raw = ms.ToArray();
        Request.Body.Position = 0;

        if (!verifier.Verify("WhatsApp", Request, raw))
        {
            return Unauthorized();   // CC-5 / CC-27 — refused before any DB call, payload not logged
        }

        var payload = JsonSerializer.Deserialize<WhatsAppWebhookPayload>(raw);   // provider-shaped DTO, not shown — Meta's nested entry/changes/messages structure
        if (payload is null) return BadRequest();

        var result = await mediator.Send(new IngestInboundChannelMessageCommand(
            "WhatsApp", payload.SenderName, payload.From, CustomerEmail: null, payload.Text, payload.MessageId), ct);

        return Ok();   // webhook contract: 200 regardless of downstream outcome, or the provider retries forever
    }
}
```

**Reply path.** Extends `RecordTicketMessageCommandHandler` (read in full above) rather than
creating a parallel one — the shape it already has (load ticket, create outbound `TicketMessage`)
just gains a dispatch call when the target channel isn't `System`:

```csharp
// inside RecordTicketMessageCommandHandler.Handle, after TicketMessage.Create for an Outbound message
// whose Channel resolves to a real transport (WhatsApp/SMS, not Email/System which have their own paths already):
if (request.Direction == "Outbound" && (request.Channel == "WhatsApp" || request.Channel == "SMS"))
{
    await notificationGateway.SendAsync(new NotificationDispatchRequest(
        TemplateCode: "TICKET_REPLY",
        RecipientUserId: null,
        Channels: [NotificationChannel.Create(request.Channel)],
        Variables: new Dictionary<string, string> { ["Body"] = request.Body },
        Email: null,
        PhoneNumber: resolvedCustomerPhone,   // loaded from the ticket's Customer
        BypassUserSettings: true,             // A8 — a direct reply is not a system notification with a preference toggle
        DeduplicationKey: null,
        CorrelationId: request.TicketId.ToString()), ct);
}
```

**Run (when implemented):** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~WhatsApp"`
**Expected:** Outbound dispatch calls the configured URL only; a retried webhook with the same
`MessageId` produces exactly one stored message; an unsigned webhook is refused with no write.
**Commit (when implemented):** `feat: add WhatsApp channel sender and inbound webhook`

### Task 3 — SMS conversations

**Covers:** `CC-11`..`CC-13`.

`Infrastructure/Channels/TwilioSignatureVerifier.cs` — same `IWebhookSignatureVerifier` contract as
`MetaSignatureVerifier`, different algorithm (Twilio signs `X-Twilio-Signature` as
`Base64(HMAC-SHA1(url + sorted-form-params, authToken))`, not a raw-body HMAC — this is the one
provider-specific detail that cannot be copy-pasted from Task 2 and must be implemented against
Twilio's actual documented algorithm when this task is executed, not assumed identical to Meta's).

`ExternalApi/Controllers/SmsWebhookController.cs` — structurally identical to
`WhatsAppWebhookController` above (verify → parse → `IngestInboundChannelMessageCommand` with
`Channel = "SMS"`, `CustomerPhone` from the provider's `From` field).

The reply path needs **no new code** — `SmsNotificationChannelSender` (read in full above) already
exists and is already resolved by `NotificationDispatcher` for `NotificationChannel.Sms`; the
`RecordTicketMessageCommandHandler` extension from Task 2 already branches on `request.Channel ==
"SMS"` using the same `NotificationChannel.Create(request.Channel)` call. Task 3 is webhook-only.

**Run (when implemented):** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~SmsConversation"`
**Expected:** Inbound SMS creates/appends a ticket exactly like WhatsApp's path; a ticket reply to an
SMS-sourced ticket sends by SMS through the already-existing sender.
**Commit (when implemented):** `feat: add inbound SMS conversation ingestion`

### Task 4 — Live chat

**Covers:** `CC-14`..`CC-19` (backend), plus the API seams `CC-25`/`CC-26` need.

**`Domain/Entities/Channels/LiveChatSession.cs`** — same shape as `Ticket.cs`'s lifecycle methods
(private setters, guard-exception on an illegal transition, `Append`-style domain events optional):

```csharp
namespace CustomerSupport.Domain.Entities.Channels;

public class LiveChatSession : AggregateRoot
{
    public string Status { get; private set; } = "Waiting";
    public string? CustomerName { get; private set; }
    public string? CustomerContact { get; private set; }
    public string SessionToken { get; private set; } = string.Empty;
    public Guid? AssignedAgentId { get; private set; }
    public Guid? TicketId { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    public static LiveChatSession Start(string? customerName, string? customerContact)
    {
        return new LiveChatSession
        {
            Id = Guid.NewGuid(),
            Status = "Waiting",
            CustomerName = customerName,
            CustomerContact = customerContact,
            SessionToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Claim(Guid agentId)
    {
        if (Status != "Waiting")
            throw new InvalidOperationException($"Session '{Id}' cannot be claimed from status '{Status}'.");
        if (agentId == Guid.Empty)
            throw new ArgumentException("An agent is required", nameof(agentId));

        AssignedAgentId = agentId;
        Status = "Active";
        MarkUpdated();
    }

    public void Close()
    {
        if (Status != "Active")
            throw new InvalidOperationException($"Session '{Id}' cannot be closed from status '{Status}'.");
        Status = "Closed";
        ClosedAt = DateTime.UtcNow;
        MarkUpdated();
    }

    public void Abandon()
    {
        if (Status != "Waiting")
            throw new InvalidOperationException($"Session '{Id}' cannot be abandoned from status '{Status}'.");
        Status = "Abandoned";
        ClosedAt = DateTime.UtcNow;
        MarkUpdated();
    }

    public void LinkTicket(Guid ticketId) => TicketId = ticketId;
}
```

**`Domain/Entities/Channels/LiveChatMessage.cs`** — same append-only shape as `TicketMessage.cs`:

```csharp
public class LiveChatMessage : BaseEntity, IAppendOnlyEntity
{
    public Guid SessionId { get; private set; }
    public string SenderType { get; private set; } = string.Empty;   // "Customer" | "Agent"
    public string Body { get; private set; } = string.Empty;
    public DateTime SentAt { get; private set; }

    public static LiveChatMessage Create(Guid sessionId, string senderType, string body)
    {
        if (senderType is not ("Customer" or "Agent"))
            throw new ArgumentException("SenderType must be Customer or Agent", nameof(senderType));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required", nameof(body));

        return new LiveChatMessage
        {
            SessionId = sessionId, SenderType = senderType, Body = body.Trim(), SentAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow
        };
    }
}
```

Both implement `IAppendOnlyEntity` (`Domain/Common/IAppendOnlyEntity.cs`, read in full above — a
one-line marker), so `AppDbContext.GuardAppendOnlyHistory()` covers `LiveChatMessage` with **zero
change to `AppDbContext` itself**, exactly as the conversation-record spec's Design section already
proved for `TicketMessage` (`ChangeTracker.Entries<IAppendOnlyEntity>()` is already generic).

**`Api.Shared/Hubs/ChatHub.cs`** — a second, narrow hub, deliberately not `MainHub`. Confirmed real
constraint: `Api.Shared/Extensions/WebApplicationExtensions.cs:70` currently reads
`app.MapHub<MainHub>("/hubs/main").RequireAuthorization("Authenticated");` — an anonymous chat
visitor cannot satisfy that policy, and loosening it would let an anonymous connection reach the
`user:{id}` groups `FEAT-15`'s in-app notifications rely on (spec `A12`).

```csharp
namespace CustomerSupport.Api.Shared.Hubs;

public class ChatHub(ILiveChatSessionLookup sessions, ILogger<ChatHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var token = Context.GetHttpContext()?.Request.Query["token"].ToString();
        var session = string.IsNullOrEmpty(token) ? null : await sessions.FindByTokenAsync(token!);

        if (session is null || session.Status is not ("Waiting" or "Active"))
        {
            Context.Abort();   // CC-19 — no token, unknown token, or a Closed/Abandoned session
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat:{session.Id}");
        await base.OnConnectedAsync();
    }
}
```

`ILiveChatSessionLookup` is a small read-only port (`Application/Channels/Contracts.cs`) implemented
in `Infrastructure` against `IRepository<LiveChatSession>`, so `Api.Shared` (which owns the hub, same
placement reasoning as `IRealTimeNotifier`/`RealTimeNotifier` for in-app notifications) doesn't need
a direct `AppDbContext` dependency.

`WebApplicationExtensions.cs` — one line added **next to**, not instead of, the existing mapping:

```csharp
app.MapHub<CustomerSupport.Api.Shared.Hubs.MainHub>("/hubs/main").RequireAuthorization("Authenticated");
app.MapHub<CustomerSupport.Api.Shared.Hubs.ChatHub>("/hubs/chat");   // ADD — no RequireAuthorization: anonymous by design (A12)
```

Agents keep using the existing `MainHub` and its already-generic `JoinGroup(string)` method
(`MainHub.cs:39-43`, read above — takes any group name, no change needed) to join `chat:{sessionId}`
once they claim a session from the `InternalApi` side.

**Commands** (`Application/Features/Channels/Commands/{StartChatSession,ClaimChatSession,
SendChatMessage,EndChatSession,ConvertChatSessionToTicket}/`) follow the same
command/handler/validator triplet shown in full for `IngestInboundChannelMessage` above — not
reproduced five more times here. The one with real logic beyond a straightforward entity-method call
is conversion:

```csharp
// ConvertChatSessionToTicketCommandHandler — sketch of the transcript copy, the part CC-17 actually tests
var chatMessages = await liveChatMessages.ListOrderedAsync(m => m.SessionId == session.Id, m => m.SentAt, descending: false, ct);
foreach (var cm in chatMessages)
{
    var senderId = cm.SenderType == "Agent" ? session.AssignedAgentId!.Value : SystemActors.ChannelIngestion;
    var ticketMessage = TicketMessage.Create(
        ticket.Id, direction: cm.SenderType == "Agent" ? "Outbound" : "Inbound",
        channel: "LiveChat", subject: null, body: cm.Body, senderId: senderId);
    await ticketMessages.AddAsync(ticketMessage, ct);
}
session.LinkTicket(ticket.Id);
```

**Run (when implemented):** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~LiveChat"`
**Expected:** A claimed session's messages reach both parties over `chat:{sessionId}`; an unclaimed
session past the timeout becomes `Abandoned`, never auto-converted; a token from session A cannot
join session B's group; a converted session's transcript lands as ordered `TicketMessage` rows.
**Commit (when implemented):** `feat: add live chat sessions, hub, and ticket conversion`

### Task 5 — Web forms

**Covers:** `CC-20`..`CC-23`.

```csharp
// Application/Features/Channels/Commands/SubmitWebFormTicket/SubmitWebFormTicketCommandHandler.cs
public class SubmitWebFormTicketCommandHandler(
    IWebFormRateLimiter rateLimiter,
    IMediator mediator,   // delegates to IngestInboundChannelMessageCommand — no second ticket-creation path
    INotificationGateway gateway,
    ITicketReferenceLookup referenceLookup)   // resolves the created ticket's human Reference for the confirmation email
    : ICommandHandler<SubmitWebFormTicketCommand, Response<string>>
{
    public async Task<Response<string>> Handle(SubmitWebFormTicketCommand request, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(request.HoneypotField) || await rateLimiter.IsThrottledAsync(request.IpAddress, request.Email, ct))
        {
            return Response<string>.Ok("SUBMITTED");   // CC-22 — identical shape to success, nothing created, logged separately
        }

        var ingestResult = await mediator.Send(new IngestInboundChannelMessageCommand(
            "WebForm", request.Name, CustomerPhone: null, request.Email, request.Description, ProviderMessageId: null), ct);

        if (!ingestResult.Succeeded) return Response<string>.From(ingestResult);   // CC-21 — field-keyed 400s pass through unchanged

        var reference = await referenceLookup.GetReferenceAsync(ingestResult.Data, ct);

        await gateway.SendAsync(new NotificationDispatchRequest(
            TemplateCode: "WEB_FORM_CONFIRMATION", RecipientUserId: null, Channels: [NotificationChannel.Email],
            Variables: new Dictionary<string, string> { ["Reference"] = reference }, Email: request.Email,
            PhoneNumber: null, BypassUserSettings: true, DeduplicationKey: null, CorrelationId: null), ct);   // CC-23

        return Response<string>.Ok(reference);   // CC-20 — reference only, no internal ids
    }
}
```

`WebFormController` on `ExternalApi` follows the same anonymous-per-action pattern as
`KnowledgeBaseController` / `WhatsAppWebhookController` above:

```csharp
[HttpPost]
[AllowAnonymous]
public async Task<IActionResult> Submit([FromBody] WebFormSubmitRequest request, CancellationToken ct)
{
    var command = new SubmitWebFormTicketCommand(
        request.Name, request.Email, request.Subject, request.Description,
        request.HoneypotField, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    var result = await mediator.Send(command, ct);
    return this.ToActionResult(result);
}
```

`IWebFormRateLimiter` (`Infrastructure/Channels/WebFormRateLimiter.cs`) — a fixed sliding-window
counter, no new package: `IMemoryCache` (already available to every ASP.NET Core host here) keyed on
`ip:{ip}` and `email:{email}`, incremented per attempt, rejected past a configured threshold within a
configured window. No Redis, no new dependency — this project's scale does not need a distributed
limiter, and adding one would be exactly the kind of scope this plan's own discipline argues against.

**Run (when implemented):** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~WebForm"`
**Expected:** A valid submission creates a ticket and sends a confirmation; a honeypot-filled or
rate-limited submission creates nothing but responds identically; a submission failing validation
gets field-keyed `400`s.
**Commit (when implemented):** `feat: add anonymous web-form ticket submission`

### Task 6 — Cross-cutting security and evidence gate

**Covers:** `CC-27`..`CC-29`, plus re-verification of `CC-1`..`CC-26` together.

**Steps:**

1. `git grep -n "IWebhookSignatureVerifier" -- 'backend/src/**/Controllers/*.cs'` and confirm the
   verifier call precedes every `AppDbContext`/repository use in each webhook controller — a grep
   check, not a read-and-trust.
2. `git grep -n "rawBody\|payload\|RawRequest" -- 'backend/src/**/Notifications/*.cs' 'backend/src/**/Channels/*.cs'`
   piped through the logging call sites, confirming none logs a full payload, secret, or session
   token in cleartext.
3. Confirm no anonymous `ExternalApi` route in `WhatsAppWebhookController`, `SmsWebhookController`,
   `WebFormController`, `LiveChatController`, or `ChatHub` accepts a client-supplied
   `CustomerId`/`TicketId`/`SessionId` and trusts it without the matching this spec defines.
4. Run the full focused test suite for these features together, plus
   `dotnet build backend/CustomerSupport.slnx --warnaserror`.
5. Update this plan's `README.md` and each task file's status from **observed** output only.

**Run (when implemented):** `dotnet build backend/CustomerSupport.slnx --warnaserror` then
`dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~Channel|WhatsApp|LiveChat|WebForm"`
**Expected:** Clean build; all focused tests for `FEAT-24`..`FEAT-27` pass.
**Commit (when implemented):** `feat: complete communication channels evidence gate`

## Explicitly not tasked here

- The frontend plan (message-timeline channel badges, live-chat queue/session screens, web-form
  widget) — written once Tasks 1–6 are implemented, per the SDD gate.
- The Playwright journey — `AC-64` is terminal and singular per `FEAT-11`; not amended here.
- Anything in [Out of scope](../../specs/EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md#out-of-scope)
  of the spec.
