# EPIC-03 · Communication channels

| | |
|---|---|
| **Epic** | `EPIC-03` |
| **Priority** | P1 (email and SMS gateway) · reopened 2026-08-27 for spec + plan (WhatsApp, live chat, web forms, SMS conversations) — see below |
| **Stories** | US-201–US-205 plus the Sprint 9 notification-gateway delivery boundary; US-230–US-240 (to be filed) for Sprint 16 |
| **Sprints** | 6 (message record) · 9 (notification gateway, email and SMS, slice S5) · 10 (web forms, via portal S3) · 16 (WhatsApp/live chat/web forms/SMS conversations, reopened) |

## Goal

Allow customer interactions to be managed through supported communication channels *(rule
specification §8)*.

## Status: not specified

No story here has a spec, so none carries acceptance criteria or an id allocation beyond the rule
file's reservations. The rule spec §8 is explicit that each channel "requires detailed discovery"
before implementation; that discovery is registered, not guessed:

- **Email** — the one channel with committed delivery. The *message record* (`FR-3.4`) is pulled
  forward to **sprint 6** because SLA measurement, portal replies and AI summaries all need it
  (`G-3`, `RSK-2`); the *provider integration* follows at **sprint 9**, blocked on `DEP-1`.
  Whether inbound mail creates or updates tickets is `OQ-11`.
- **Web forms** — arrive with the customer portal, sprint 10 (`US-404`, authenticated), **and** as a
  separate anonymous public intake surface under sprint 16 (see below) — the two are distinct flows,
  not duplicates; see the sprint-16 spec's `A4`.
- **WhatsApp / Live Chat / SMS conversations** — were **deferred indefinitely** with stated reasons
  (paid provider, verified business identity, rostered staffing): BRD §6.3. **Reopened 2026-08-27 at
  explicit request** for spec + backend plan only (sprint 16, below) — the stated reasons are not
  resolved, they are carried forward as open questions the business must close before any production
  deployment. Nothing is implemented.

## Sprint 16 — WhatsApp, SMS conversations, live chat, web forms (reopened 2026-08-27)

Spec: [`EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md`](../../superpowers/specs/EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md).
Plan: [`EPIC-03-US-201-feat-24-communication-channels/`](../../superpowers/plans/EPIC-03-US-201-feat-24-communication-channels/).
Covers `FEAT-24` (WhatsApp), `FEAT-25` (SMS conversations), `FEAT-26` (live chat), `FEAT-27` (web
forms — the anonymous public form, distinct from `US-404`'s authenticated portal submission). All
four extend `FEAT-15`'s `INotificationChannelSender` pattern and `FEAT-14`'s `TicketMessage`; none
of them changes `INotificationGateway`, `MainHub`, or any already-shipped channel. **Spec and
backend plan only — no code, no migration, no test exists yet**, per explicit instruction to stop at
planning. The business decisions the original deferral named (WhatsApp provider account, live-chat
staffing roster, CAPTCHA approval) remain open and are not resolved by this spec.

## Sprint 9 notification gateway

The canonical design is [`EPIC-03-US-219-notification-gateway.md`](../../superpowers/specs/EPIC-03-US-219-notification-gateway.md)
with its executable plan in [`EPIC-12-US-000-notification-gateway/`](../../superpowers/plans/EPIC-12-US-000-notification-gateway/).
It is the shared delivery boundary for US-201–US-205 and SLA/OTP notifications. Email and SMS use
the configured `EmailGateway` and `SmsGateway` integration URLs; feature handlers do not call
providers directly.

## Reserved backlog (rule-file titles — unspecified by design)

US-027 Receive Email Communication · US-028 Reply to Customer by Email · US-029 Associate Email
Communication with Ticket · US-030 Receive WhatsApp Communication · US-031 Reply Through WhatsApp ·
US-032 Manage Live Chat Conversation · US-033 Send SMS · US-034 Receive Web Form Submission

When a slice is specified, its stories are **rewritten from that slice's spec**, not edited towards
it — see [`../README.md#keeping-this-folder-honest`](../README.md#keeping-this-folder-honest).
