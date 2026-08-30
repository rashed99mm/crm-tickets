# Task 03 — Email and SMS Integration Channels

**Criteria:** `NG-2`, `NG-3`, `NG-4`, `NG-7`

## Files

- `Application/Interfaces/IEmailSender.cs`
- `Application/Interfaces/ISmsSender.cs`
- `Infrastructure/Notifications/EmailNotificationChannelSender.cs`
- `Infrastructure/Notifications/SmsNotificationChannelSender.cs`
- `Infrastructure/ServiceCollectionExtensions.cs`

## Interfaces

```csharp
public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct = default);
}

public interface ISmsSender
{
    Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken ct = default);
}
```

## Steps

1. Write fake-HTTP tests for EmailGateway and SmsGateway URL selection.
2. Load configuration via `IExternalApiConfigurationProvider`; unprotect credentials only at send time.
3. Apply timeout and bounded retry for timeout, connection reset, and 5xx responses.
4. Do not retry malformed input, 4xx authentication, or unsupported provider responses.
5. Redact provider errors before returning `EmailSendResult`/`SmsSendResult`.

**Run:** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~Email|Sms"`  
**Expected:** Both channels call only their configured URL and never log secrets or bodies.

**Commit:** `feat: route email and sms through integrations`
