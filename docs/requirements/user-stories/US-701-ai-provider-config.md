# US-701 · AI Provider Configuration

| Field | Value |
|---|---|
| **Story** | `US-701` |
| **Epic** | [EPIC-11 AI Features](../epics/EPIC-11-ai.md) |
| **Feature** | [`FEAT-20` AI-Powered Features](../delivery-plan.md#feat-20--ai-powered-features) |
| **Layer** | Backend |
| **Ships with** | [US-702](./US-702-ai-service-port.md) *(backend)* |
| **Actor** | System |
| **Priority** | P2 |
| **Sprint** | [15 — AI assist](../delivery-plan.md#sprint-15-ai-assist) · Slice S7 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-7.1 |
| **Spec criteria** | AC-701 |
| **Depends on** | [US-701](./US-701-ai-provider-config.md) |

## Story

**As a system**, **I want** the AI provider to be configurable through platform settings, **so that** AI features work regardless of which provider is selected.

## Business rules

- No BRD BR-n covers this directly. AI provider configuration is stored in PlatformSettings and includes provider type, API key, and model selection.
- No BRD BR-n covers this directly. API keys are encrypted at rest and never returned in API responses.

## Acceptance criteria

#### AC1 — AI provider settings (spec AC-701)

Given the admin configures AI provider settings, when saved, then the provider type, API key (encrypted), and model are persisted in PlatformSettings.

#### AC2 — Settings not exposed (spec AC-701)

Given AI provider settings are configured, when any user queries platform settings, then the API key field is never returned in the response.

## SQL tables

No new tables. AI configuration stored in existing `PlatformSettings` table.

```sql
-- PlatformSettings entries
INSERT INTO [dbo].[PlatformSettings] ([Key], [Value], [Encrypted]) VALUES
  ('Ai:Provider', 'openai', 0),
  ('Ai:ApiKey', 'sk-xxxx', 1),
  ('Ai:Model', 'gpt-4o', 0);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-701 | Integration | `AiProviderConfigSaved` | Given admin sets provider=openai and model=gpt-4o, when saved, then PlatformSettings contains the entries. | Settings persisted correctly |
| TC-02 | AC-701 | Integration | `AiApiKeyNotExposed` | Given an API key is configured, when any user reads platform settings, then the key value is masked or omitted. | API key never returned |

## Notes

Blocked on DEP-6 (AI infrastructure dependencies) and OQ-8 (open question on provider selection). Implementation cannot begin until these are resolved.

## Open questions

- OQ-8: Which AI providers are supported at launch? OpenAI only or multiple?

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
