# Task 12 — Email channel (out + in)

## Traceability
Epic:   docs/requirements/epics/EPIC-03-communication-channels.md
Stories: EPIC-03-US-203-email-provider.md, EPIC-03-US-204-inbound-email.md, EPIC-03-US-205-outbound-email.md
FEAT:   FEAT-15/24 — delivery-plan rows 9/16
Specs:  docs/superpowers/specs/EPIC-03-EPIC-03-US-203-email-provider.md, EPIC-03-EPIC-03-US-204-inbound-email.md,
        EPIC-03-EPIC-03-US-205-outbound-email.md
Plans:  plans/EPIC-03-US-203-email-provider/, EPIC-03-US-204-inbound-email/, EPIC-03-US-205-outbound-email/,
        EPIC-10-US-203-email-integration/

## Work
Outbound: `EmailNotificationChannelSender` (existing HTTP-based, no SMTP). Inbound:
`EmailMessageConsumer` now delegates to `INotificationGateway` instead of `Task.Delay` stub.
SMS consumer similarly wired. (No SMTP per instruction; HTTP gateway already covers outbound.)

## Gate
- [x] `EmailMessageConsumer` + `SmsMessageConsumer` delegate to `INotificationGateway`.
- [x] Backend build clean (`dotnet build CustomerSupport.slnx` succeeded 0 errors).
