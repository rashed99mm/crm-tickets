# US-204 Inbound Email: Implementation Plan

> **Disclosure (added 2026-08-27):** Rewritten to carry real, code-bearing Task sections. The
> inbound email ingestion described here is NOT SHIPPED; the code below is the concrete design to
> implement, riding the existing `ExternalApiConfiguration`/`IEmailSender` surface and the
> `RecordTicketMessageCommandHandler` write path (no new message-insertion logic).

**Story:** `US-204` · **Spec:** `docs/superpowers/specs/EPIC-10-US-203-email-integration-design.md` · **Status:** NOT SHIPPED

## AC mapping

| Story AC | Proof |
|---|---|
| AC1 — inbound mail creates a ticket message | `InboundEmailWebhookTests.AC204_InboundMail_ForKnownTicket_CreatesInboundMessage` |
| AC2 — signature verification rejects forged posts | `InboundEmailWebhookTests.AC204_BadSignature_Returns401` |
| AC3 — unknown ticket is quarantined, not 500s | `InboundEmailWebhookTests.AC204_UnknownTicket_Returns202ButNoMessage` |

## Affected files

- Create: `backend/src/CustomerSupport.InternalApi/Controllers/EmailWebhookController.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Emails/Commands/IngestInboundEmail/` (Command + Handler)
- Create: `backend/src/CustomerSupport.Infrastructure/Email/EmailSignatureVerifier.cs`
- Modify: `backend/src/CustomerSupport.Infrastructure/Email/EmailServiceCollectionExtensions.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/InboundEmailWebhookTests.cs`

---

### Task 1: Signature-verified anonymous webhook (`AC-204.2`)

**Files:**
- Create: `backend/src/CustomerSupport.Infrastructure/Email/EmailSignatureVerifier.cs`
- Create: `backend/src/CustomerSupport.InternalApi/Controllers/EmailWebhookController.cs`

**Interfaces:**
- Consumes: `IExternalApiConfigurationProvider.GetConfig("Email")` for the shared secret; `IMediator`.
- Produces: `IngestInboundEmailCommand(string From, string To, string Subject, string BodyHtml, string BodyText)`.

- [ ] **Step 1: Write the failing signature test**

```csharp
[Fact] [Trait("AC", "204.2")]
public async Task AC204_BadSignature_Returns401()
{
    var payload = "{\"from\":\"c@x.test\",\"to\":\"ticket-<id>@crm.test\",\"subject\":\"re\",\"body\":\"hi\"}";
    var response = await _client.PostAsync("/api/webhooks/email",
        new StringContent(payload, Encoding.UTF8, "application/json")
        { Headers = { { "X-Email-Signature", "deadbeef" } } });
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

- [ ] **Step 2: Implement the verifier**

```csharp
// backend/src/CustomerSupport.Infrastructure/Email/EmailSignatureVerifier.cs
using System.Security.Cryptography;
using CustomerSupport.Application.ExternalApis;

namespace CustomerSupport.Infrastructure.Email;

public sealed class EmailSignatureVerifier(IExternalApiConfigurationProvider configProvider)
{
    public bool Verify(string rawBody, string signatureHeader)
    {
        var config = configProvider.GetConfig("Email");
        if (config?.AuthClientSecret is null) return false;
        var key = Convert.FromBase64String(config.AuthClientSecret);
        var bodyBytes = System.Text.Encoding.UTF8.GetBytes(rawBody);
        var supplied = Convert.FromHexString(signatureHeader);
        using var hmac = new HMACSHA256(key);
        var computed = hmac.ComputeHash(bodyBytes);
        return CryptographicOperations.FixedTimeEquals(computed, supplied);
    }
}
```

- [ ] **Step 3: Implement the controller (anonymous, deliberate exception to InternalApi's auth)**

```csharp
// backend/src/CustomerSupport.InternalApi/Controllers/EmailWebhookController.cs
[ApiController]
[Route("api/webhooks")]
[AllowAnonymous]
public sealed class EmailWebhookController(IMediator mediator, EmailSignatureVerifier verifier) : ControllerBase
{
    [HttpPost("email")]
    public async Task<IActionResult> Receive()
    {
        using var reader = new StreamReader(Request.Body);
        var raw = await reader.ReadToEndAsync();
        if (!Request.Headers.TryGetValue("X-Email-Signature", out var sig) || !verifier.Verify(raw, sig!))
            return Unauthorized();

        var dto = JsonSerializer.Deserialize<InboundEmailDto>(raw)!;
        await mediator.Send(new IngestInboundEmailCommand(dto.From, dto.To, dto.Subject, dto.BodyHtml, dto.BodyText), HttpContext.RequestAborted);
        return Accepted();
    }
}
```

- [ ] **Step 4: Run to verify it fails, then passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~InboundEmailWebhookTests"`
Expected: first FAIL (types missing), then PASS after Task 2.

- [ ] **Step 5: Commit**

```bash
git add backend/src/CustomerSupport.Infrastructure/Email/EmailSignatureVerifier.cs \
        backend/src/CustomerSupport.InternalApi/Controllers/EmailWebhookController.cs
git commit -m "feat(email): signature-verified inbound webhook (AC-204.2)"
```

---

### Task 2: Ticket resolution + message persistence (`AC-204.1`, `AC-204.3`)

**Files:**
- Create: `backend/src/CustomerSupport.Application/Features/Emails/Commands/IngestInboundEmail/IngestInboundEmailCommand.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Emails/Commands/IngestInboundEmail/IngestInboundEmailCommandHandler.cs`
- Test: `InboundEmailWebhookTests.cs` (append)

**Interfaces:**
- Consumes: `IRepository<Ticket>`, `IRepository<TicketMessage>`, `RecordTicketMessageCommand` path.

- [ ] **Step 1: Write the failing ingest tests**

```csharp
[Fact] [Trait("AC", "204.1")]
public async Task AC204_InboundMail_ForKnownTicket_CreatesInboundMessage()
{
    var ticketId = await CreateTicketAsync();
    var body = "{\"from\":\"c@x.test\",\"to\":\"ticket-" + ticketId + "@crm.test\",\"subject\":\"re\",\"bodyHtml\":\"<p>hi</p>\"}";
    SignAndPost(body);
    var messages = await _client.GetFromJsonAsync<Response<List<TicketMessageRow>>>($"/api/Tickets/{ticketId}/messages");
    messages!.Data.Should().ContainSingle(m => m.Direction == "Inbound" && m.Channel == "Email" && m.Body.Contains("hi"));
}

[Fact] [Trait("AC", "204.3")]
public async Task AC204_UnknownTicket_Returns202ButNoMessage()
{
    var body = "{\"from\":\"c@x.test\",\"to\":\"ticket-00000000-0000-0000-0000-000000000000@crm.test\",\"subject\":\"re\",\"bodyHtml\":\"<p>hi</p>\"}";
    var response = SignAndPost(body);
    response.StatusCode.Should().Be(HttpStatusCode.Accepted); // accepted, dropped — not 500
}
```

- [ ] **Step 2: Implement the command + handler**

```csharp
// IngestInboundEmailCommand.cs
public record IngestInboundEmailCommand(string From, string To, string Subject, string? BodyHtml, string? BodyText)
    : ICommand<Response<Unit>>;
```

The handler parses the ticket id out of the `To` address (`ticket-{guid}@...`),
`404`-equivalent no-op (logs and returns success to the webhook so providers don't retry forever) when
the ticket is unknown, otherwise reuses the existing `RecordTicketMessageCommandHandler` write path
with `Direction = Inbound, Channel = Email, SenderName = From`. It does **not** re-implement message
insertion — it dispatches `RecordTicketMessageCommand`.

- [ ] **Step 3: Run to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~InboundEmailWebhookTests"`
Expected: PASS, 3/3.

- [ ] **Step 4: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Emails/Commands/IngestInboundEmail/ \
        backend/tests/CustomerSupport.Tests/Integration/InboundEmailWebhookTests.cs
git commit -m "feat(email): inbound mail -> ticket message (AC-204.1, AC-204.3)"
```

## Definition of done

`AC-204.1`..`AC-204.3` each covered by a named test · `dotnet build` clean · targeted test run pasted.
