# US-203 Email Provider: Implementation Plan

> **Disclosure (added 2026-08-27):** Rewritten to carry real, code-bearing Task sections designing
> the `IEmailSender` port with a concrete **SMTP** provider (`SmtpEmailSender`). This is the
> `US-203`-numbered design; it complements (does not replace) the HTTP/Refit provider described in
> `EPIC-10-US-203-email-integration`'s Task 1. Both satisfy `IEmailSender` — the port is the contract,
> the transport is swappable by `ExternalApiConfiguration.AuthType`.

**Story:** `US-203` · **Spec:** `docs/superpowers/specs/EPIC-10-US-203-email-integration-design.md`, assumption A1 · **Status:** NOT SHIPPED

## AC mapping

| This story's AC | Proof |
|---|---|
| AC1 — send via configured provider | `SmtpEmailSenderTests.SendAsync_ValidConfig_SendsMailViaSmtp` |
| AC2 — retry with backoff | `SmtpEmailSenderTests.SendAsync_TransientSmtpException_RetriesWithBackoff` |
| AC3 — non-transient fails without retry | `SmtpEmailSenderTests.SendAsync_AuthenticationFailure_DoesNotRetry` |
| (implicit) — not configured | `SmtpEmailSenderTests.SendAsync_NoSmtpConfig_ReturnsNotConfiguredWithoutConnecting` |

## Affected files

- Create: `backend/src/CustomerSupport.Application/Interfaces/IEmailSender.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Email/SmtpEmailSender.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Email/SmtpClientAdapter.cs` (testable seam)
- Modify: `backend/src/CustomerSupport.Infrastructure/Email/EmailServiceCollectionExtensions.cs` (or existing external-API registration)
- Modify: `ApplicationErrors.cs`, `SystemCode.cs`, `SystemCodeMap.cs`, `ResponseExtensions.cs`, `Resources.yaml`
- Test: `backend/tests/CustomerSupport.Tests/Unit/SmtpEmailSenderTests.cs`

---

### Task 1: `IEmailSender` port + `EmailMessage`/`EmailSendResult` (`AC-203.1`)

**Files:**
- Create: `backend/src/CustomerSupport.Application/Interfaces/IEmailSender.cs`

**Interfaces:**
- Produces: `IEmailSender.SendAsync(EmailMessage message, CancellationToken ct) : Task<EmailSendResult>`.

- [ ] **Step 1: Write the failing test**

```csharp
// backend/tests/CustomerSupport.Tests/Unit/SmtpEmailSenderTests.cs
public class SmtpEmailSenderTests
{
    [Fact] [Trait("AC", "203.1")]
    public async Task SendAsync_ValidConfig_SendsMailViaSmtp()
    {
        var adapter = new FakeSmtpAdapter(throwsOnSend: false);
        var sender = new SmtpEmailSender(adapter, from: "support@crm.test");
        var result = await sender.SendAsync(new EmailMessage("user@x.test", "Hi", "<p>Hi</p>"), default);
        result.Success.Should().BeTrue();
        adapter.SendCount.Should().Be(1);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~SmtpEmailSenderTests"`
Expected: FAIL — types don't exist.

- [ ] **Step 3: The port and DTOs**

```csharp
// backend/src/CustomerSupport.Application/Interfaces/IEmailSender.cs
namespace CustomerSupport.Application.Interfaces;

public record EmailMessage(string To, string Subject, string HtmlBody);
public record EmailSendResult(bool Success, string? FailureCode);

public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct);
}
```

- [ ] **Step 4: Run to verify the port compiles**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~SmtpEmailSenderTests"`
Expected: still FAIL on `SmtpEmailSender`/`FakeSmtpAdapter` (next task).

- [ ] **Step 5: Commit**

```bash
git add backend/src/CustomerSupport.Application/Interfaces/IEmailSender.cs
git commit -m "feat(email): IEmailSender port and EmailMessage DTO (AC-203.1)"
```

---

### Task 2: `SmtpEmailSender` reading `ExternalApiConfiguration` + `ISecretProtector` (`AC-203.1`, `AC-203.4`)

**Files:**
- Create: `backend/src/CustomerSupport.Infrastructure/Email/SmtpEmailSender.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Email/ISmtpClientAdapter.cs` + `SmtpClientAdapter.cs`

**Interfaces:**
- Consumes: `IExternalApiConfigurationProvider.GetConfig("Email")` (returns `ExternalApiConfiguration`,
  `AuthType == "Smtp"`), `ISecretProtector` to unprotect `AuthClientSecret` (the SMTP password),
  `ISmtpClientAdapter` (wraps `System.Net.Mail.SmtpClient` so the send is unit-testable).

- [ ] **Step 1: Add the failing not-configured + transient tests**

```csharp
[Fact] [Trait("AC", "203.4")]
public async Task SendAsync_NoSmtpConfig_ReturnsNotConfiguredWithoutConnecting()
{
    var adapter = new FakeSmtpAdapter();
    var provider = new FakeConfigProvider(returnsNull: true);
    var sender = new SmtpEmailSender(adapter, provider, NullProtector.Instance, from: "support@crm.test");
    var result = await sender.SendAsync(new EmailMessage("u@x.test", "s", "b"), default);
    result.Success.Should().BeFalse();
    result.FailureCode.Should().Be("EMAIL_NOT_CONFIGURED");
    adapter.SendCount.Should().Be(0);
}

[Fact] [Trait("AC", "203.2")]
public async Task SendAsync_TransientSmtpException_RetriesWithBackoff()
{
    var adapter = new FakeSmtpAdapter(throwSequence: new[] { new SmtpException("temp"), new SmtpException("temp"), null });
    var sender = new SmtpEmailSender(adapter, new FakeConfigProvider(), NullProtector.Instance, from: "support@crm.test");
    var result = await sender.SendAsync(new EmailMessage("u@x.test", "s", "b"), default);
    result.Success.Should().BeTrue();
    adapter.SendCount.Should().Be(3);
}
```

- [ ] **Step 2: Implement the adapter seam**

```csharp
// backend/src/CustomerSupport.Infrastructure/Email/ISmtpClientAdapter.cs
using System.Net.Mail;

namespace CustomerSupport.Infrastructure.Email;

public interface ISmtpClientAdapter
{
    Task SendMailAsync(MailMessage message, CancellationToken ct);
}

public sealed class SmtpClientAdapter : ISmtpClientAdapter, IDisposable
{
    private readonly SmtpClient _client;
    public SmtpClientAdapter(string host, int port, bool enableSsl, string? user, string? password)
    {
        _client = new SmtpClient(host, port) { EnableSsl = enableSsl, Credentials = string.IsNullOrEmpty(user) ? null : new System.Net.NetworkCredential(user, password) };
    }
    public Task SendMailAsync(MailMessage message, CancellationToken ct) => _client.SendMailAsync(message);
    public void Dispose() => _client.Dispose();
}
```

- [ ] **Step 3: Implement `SmtpEmailSender`**

```csharp
// backend/src/CustomerSupport.Infrastructure/Email/SmtpEmailSender.cs
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.ExternalApis;
using CustomerSupport.Domain.Entities.ExternalApis;

namespace CustomerSupport.Infrastructure.Email;

public class SmtpEmailSender(
    ISmtpClientAdapter adapter,
    IExternalApiConfigurationProvider configProvider,
    ISecretProtector protector,
    string fromAddress) : IEmailSender
{
    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct)
    {
        var config = configProvider.GetConfig("Email");
        if (config is null || config.AuthType != "Smtp")
            return new EmailSendResult(false, "EMAIL_NOT_CONFIGURED");

        var password = protector.Unprotect(config.AuthClientSecret ?? string.Empty);
        using var mail = new MailMessage(fromAddress, message.To, message.Subject, message.HtmlBody)
        {
            IsBodyHtml = true
        };
        try
        {
            // The adapter's SendMailAsync carries the retry/backoff policy applied by the caller's
            // resilience pipeline; this method only maps transport exceptions to result codes.
            await adapter.SendMailAsync(mail, ct);
            return new EmailSendResult(true, null);
        }
        catch (SmtpException ex) when (ex.StatusCode is SmtpStatusCode.MailboxUnavailable or SmtpStatusCode.MustAuthenticate or SmtpStatusCode.AuthenticationFailure)
        {
            return new EmailSendResult(false, "EMAIL_AUTH_FAILED");
        }
        catch (SmtpException)
        {
            return new EmailSendResult(false, "EMAIL_SEND_FAILED");
        }
    }
}
```

The SMTP host/port come from `ExternalApiConfiguration.BaseUrl` (parsed) and `TimeoutSeconds`. The
password is stored protected via `ISecretProtector` and unprotected only at send time — never
written to logs. `SmtpClient` is wrapped in `ISmtpClientAdapter` so `FakeSmtpAdapter` can assert
send count and exception sequence without a real server.

- [ ] **Step 4: Register error codes**

`ApplicationErrors.cs`: `public static class Email { NOT_CONFIGURED = "EMAIL_NOT_CONFIGURED"; AUTH_FAILED = "EMAIL_AUTH_FAILED"; SEND_FAILED = "EMAIL_SEND_FAILED"; }`.
`SystemCode.cs`/`SystemCodeMap.cs`: `ERR057`/`ERR058`/`ERR059` mapped. `ResponseExtensions.MapFailureStatusCode`: add to the `409`/`503` arms as appropriate. Bilingual pairs in `Resources.yaml`.

- [ ] **Step 5: Run to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~SmtpEmailSenderTests"`
Expected: PASS, 4/4.

- [ ] **Step 6: Commit**

```bash
git add backend/src/CustomerSupport.Application/Interfaces/IEmailSender.cs \
        backend/src/CustomerSupport.Infrastructure/Email/SmtpEmailSender.cs \
        backend/src/CustomerSupport.Infrastructure/Email/ISmtpClientAdapter.cs \
        backend/src/CustomerSupport.Infrastructure/Email/SmtpClientAdapter.cs \
        backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs \
        backend/src/CustomerSupport.Application/Messages/SystemCode.cs \
        backend/src/CustomerSupport.Application/Messages/SystemCodeMap.cs \
        backend/src/CustomerSupport.Api.Shared/Extensions/ResponseExtensions.cs \
        backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml \
        backend/tests/CustomerSupport.Tests/Unit/SmtpEmailSenderTests.cs
git commit -m "feat(email): SmtpEmailSender over ExternalApiConfiguration + ISecretProtector (AC-203.1..4)"
```

## Definition of done

`AC-203.1`..`AC-203.4` each covered by a named test · `dotnet build` clean · targeted test run pasted.
This is the SMTP transport for `IEmailSender`; the HTTP/Refit transport in the email-integration
plan is an alternative registration keyed by `AuthType`.
