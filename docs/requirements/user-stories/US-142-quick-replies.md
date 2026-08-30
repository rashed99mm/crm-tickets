# US-142 · Quick replies

| Field | Value |
|---|---|
| **Story** | `US-142` |
| **Epic** | [EPIC-04 Agent workspace](../epics/EPIC-04-agent-workspace.md) |
| **Feature** | `FEAT-28` Agent workspace tasks |
| **Layer** | Both |
| **Actor** | Support Agent |
| **Priority** | P1 |
| **Estimate** | 2 points |
| **Status** | `done` |

## Story

**As a support agent**, **I want** to select a pre-written quick reply and insert it into the ticket reply composer, **so that** I can respond faster with consistent messaging.

## Acceptance criteria

#### AC-142.1 — Seeded quick reply catalogue

Given the system, when it starts, then a set of standard quick replies (shortcut + body) is seeded, covering common greetings, acknowledgements, and closings.

#### AC-142.2 — Quick reply picker in composer

Given a ticket reply composer, when I open the quick reply picker, then I see the list of shortcuts and bodies and can search or scroll.

#### AC-142.3 — Insert into composer

Given a quick reply is selected, when I confirm, then its body text is inserted at the cursor position in the reply composer, editable before sending.

## SQL tables

```sql
QuickReplies(Id, Shortcut, Body, CreatedAt)
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then |
|---|---|---|---|---|
| TC-01 | AC-142.1 | Integration | `QuickReplies_SeededOnStartup` | Given the migrator runs, when checked, then at least 5 quick replies exist |
| TC-02 | AC-142.2 | Component | `QuickReplyPicker_ShowsList` | Given the picker is open, when rendered, then shortcuts and bodies are shown |
| TC-03 | AC-142.3 | Component | `QuickReplyPicker_InsertsText` | Given a quick reply is selected, when confirmed, then the body text is in the composer |

## Open questions

None.

## Status evidence

Backend: `QuickReply` entity + seeded via `IMigrator` `IStartupSeed`. Read-only query handler.
Frontend: quick reply picker component on ticket reply composer.
