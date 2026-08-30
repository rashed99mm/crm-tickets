# US-205 · Outbound email reply from ticket

| Field | Value |
|---|---|
| **Story** | `US-205` |
| **Epic** | [EPIC-03 Communication channels](../epics/EPIC-03-communication-channels.md) |
| **Feature** | *(no frontend feature — triggered by agent action on ticket)* |
| **Layer** | Backend |
| **Ships with** | No frontend counterpart (triggered by agent action on ticket) |
| **Actor** | Support Agent |
| **Priority** | P0 |
| **Sprint** | [9 — Email channel](../delivery-plan.md#sprint-9--email-channel) · Slice S5 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-3.3 |
| **Spec criteria** | AC-3.3 |
| **Depends on** | [US-201](./US-201-record-message.md), [US-203](./EPIC-03-US-203-email-provider.md) |

## Story

**As a support agent**, **I want** to reply to a customer by email from the ticket, **so that** communication is tracked.

## Business rules

- BR-8 — Reference format TKT-nnnnnn: the outbound email subject includes the ticket reference tag in TKT-nnnnnn format for inbound threading (BRD).
- BR-19 — No AI auto-send without human: every outbound email is sent explicitly by an agent, never by the system without human review (BRD).

## Acceptance criteria

#### AC1 — Send reply and record message (spec AC-3.3)

Given an agent composes and sends a reply on a ticket, when the send completes, then the email is delivered to the customer and a message record with `Direction='Outbound'` is stored against the ticket.

#### AC2 — Subject includes ticket reference (spec AC-3.3)

Given an outbound email is generated, when the email is sent, then the subject line contains the ticket reference tag (e.g., `[TKT-nnnnnn]`).

#### AC3 — Send failure handling

Given email delivery fails, when the error is transient, then the message record is not created and the agent is notified of the failure; on retry success, the message is recorded.

## SQL tables

No new tables. Writes to `TicketMessages` (US-201). Uses `EmailProviderConfigurations` (US-203).

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-3.3 | Unit | `SendReply_EmailDelivered_MessageRecorded` | Given a valid ticket and agent reply body, when send completes, then email sent and message row created | Email provider receives send call; TicketMessages row with Direction='Outbound' exists |
| TC-02 | AC-3.3 | Unit | `SendReply_SubjectContainsTicketRef` | Given ticket TKT-000001, when reply email is composed, then subject contains `[TKT-000001]` | Subject line starts with the ticket reference tag |
| TC-03 | AC-3.3 | Unit | `SendReply_TransientFailure_NoMessageRecorded` | Given provider throws transient error, when send fails, then no message row and agent gets failure notification | TicketMessages unchanged; error response returned |
| TC-04 | AC-3.3 | Unit | `SendReply_RetrySuccess_MessageRecorded` | Given provider fails once then succeeds on retry, when send completes, then message row created with correct data | Single message row; no duplicates |

## Notes

- The reply API endpoint is called from the agent workspace frontend (US-022 or equivalent) but the backend logic is self-contained here.
- The ticket reference tag format must match what US-204 expects for inbound threading.
- Consider supporting CC/BCC recipients in a follow-up; not required for MVP.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
