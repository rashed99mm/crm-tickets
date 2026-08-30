# US-205 Outbound Email: Implementation Plan

> **Disclosure (added 2026-08-27):** Rewritten to carry real, code-bearing Task sections. The
> outbound email send described here is NOT SHIPPED; the code below is the concrete design, reusing
> the `IEmailSender` port from `US-203` and the existing `RecordTicketMessageCommandHandler` write
> path (no new message-insertion logic).

**Story:** `US-205` · **Spec:** `docs/superpowers/specs/EPIC-10-US-203-email-integration-design.md` · **Status:** NOT SHIPPED

## AC mapping

| Story AC | Proof |
|---|---|
| AC1 — agent reply with Channel=Email sends a real email | `OutboundEmailTests.AC205_AgentReply_EmailChannel_SendsViaSender` |
| AC2 — system/notes channel does NOT send email | `OutboundEmailTests.AC205_SystemChannel_NoEmailSent` |
| AC3 — send failure is recorded but the message is still persisted | `OutboundEmailTests.AC205_SendFails_MessageStillPersisted` |

## Affected files

- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/RecordTicketMessage/RecordTicketMessageCommandHandler.cs`
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/RecordTicketMessage/RecordTicketMessageCommand.cs` (add resolver for customer email)
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/RecordTicketMessage/SendOutboundEmailDecorator.cs` (or inline in handler)
- Test: `backend/tests/CustomerSupport.Tests/Integration/OutboundEmailTests.cs`

---

### Task 1: Send on `Channel = Email` (`AC-205.1`)

**Files:**
- Modify: `RecordTicketMessageCommandHandler.cs`
- Modify: `RecordTicketMessageCommand.cs` (carrier: `Channel` already present)

**Interfaces:**
- Consumes: `IEmailSender` (from US-203), `IRepository<Ticket>`, `IRepository<Customer>`.
- Produces: an outbound SMTP/HTTP send when `command.Channel == "Email" && command.Direction == "Outbound"`.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact] [Trait("AC", "205.1")]
public async Task AC205_AgentReply_EmailChannel_SendsViaSender()
{
    var ticketId = await CreateTicketWithCustomerAsync(email: "customer@x.test");
    var sender = Substitute.For<IEmailSender>();
    // register fake sender via factory override ...
    await _client.PostAsJsonAsync($"/api/Tickets/{ticketId}/messages",
        new { direction = "Outbound", channel = "Email", subject = "Reply", body = "Hello" });
    await sender.Received(1).SendAsync(Arg.Is<EmailMessage>(m => m.To == "customer@x.test"), Arg.Any<CancellationToken>());
}
```

- [ ] **Step 2: Extend the command + handler**

```csharp
// RecordTicketMessageCommand.cs — already carries Direction/Channel; no change needed beyond ensuring
// Channel == "Email" is accepted by the existing validator.
```

```csharp
// Inside RecordTicketMessageCommandHandler.Handle, after persisting the message row:
if (request.Direction == "Outbound" && request.Channel == "Email")
{
    var customer = await customerRepository.GetByIdAsync(ticket.CustomerId, ct);
    if (customer?.Email is { } to)
    {
        var sendResult = await emailSender.SendAsync(
            new EmailMessage(to, request.Subject ?? "(no subject)", request.Body), ct);
        // outcome recorded on the message row, never blocking persistence of the agent's reply.
        ticketMessage.MarkEmailDispatched(sendResult.Success, sendResult.FailureCode);
    }
}
```

`MarkEmailDispatched` is a new, additive method on `TicketMessage` (sets `EmailSentAt` /
`EmailFailureCode`) — the message is **always** saved first, so a send failure (AC-205.3) is visible
as a row with a failure code, not a lost reply.

- [ ] **Step 3: Run to verify it fails, then passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~OutboundEmailTests"`
Expected: PASS after wiring.

- [ ] **Step 4: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Tickets/Commands/RecordTicketMessage/ \
        backend/tests/CustomerSupport.Tests/Integration/OutboundEmailTests.cs
git commit -m "feat(email): agent reply on Email channel dispatches via IEmailSender (AC-205.1)"
```

---

### Task 2: Channel gating + failure visibility (`AC-205.2`, `AC-205.3`)

**Files:**
- Test: `OutboundEmailTests.cs` (append)

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] [Trait("AC", "205.2")]
public async Task AC205_SystemChannel_NoEmailSent()
{
    var ticketId = await CreateTicketWithCustomerAsync(email: "customer@x.test");
    await _client.PostAsJsonAsync($"/api/Tickets/{ticketId}/messages",
        new { direction = "Outbound", channel = "System", subject = "", body = "note" });
    await sender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
}

[Fact] [Trait("AC", "205.3")]
public async Task AC205_SendFails_MessageStillPersisted()
{
    var ticketId = await CreateTicketWithCustomerAsync(email: "customer@x.test");
    sender.SendAsync(...) returns new EmailSendResult(false, "EMAIL_SEND_FAILED");
    var response = await _client.PostAsJsonAsync($"/api/Tickets/{ticketId}/messages",
        new { direction = "Outbound", channel = "Email", subject = "s", body = "b" });
    response.StatusCode.Should().Be(HttpStatusCode.OK); // message saved
    var messages = await _client.GetFromJsonAsync<Response<List<TicketMessageRow>>>($"/api/Tickets/{ticketId}/messages");
    messages!.Data!.Single().EmailFailureCode.Should().Be("EMAIL_SEND_FAILED");
}
```

- [ ] **Step 2: Implement** (channel gate already in Task 1's `if`; failure code persisted by `MarkEmailDispatched`).

- [ ] **Step 3: Run to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~OutboundEmailTests"`
Expected: PASS, 3/3.

- [ ] **Step 4: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Tickets/Commands/RecordTicketMessage/ \
        backend/tests/CustomerSupport.Tests/Integration/OutboundEmailTests.cs
git commit -m "feat(email): channel gating and send-failure visibility (AC-205.2, AC-205.3)"
```

## Definition of done

`AC-205.1`..`AC-205.3` each covered by a named test · `dotnet build` clean · targeted test run pasted.
The agent-facing UI (`ticket-messages.component.ts`) already exposes a `Channel` selector; no
frontend change is required for this story beyond the existing `Email` option.
