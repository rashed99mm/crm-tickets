# US-702 · AI Service Port (Interface + Implementation)

| Field | Value |
|---|---|
| **Story** | `US-702` |
| **Epic** | [EPIC-11 AI Features](../epics/EPIC-11-ai.md) |
| **Feature** | [`FEAT-20` AI-Powered Features](../delivery-plan.md#feat-20--ai-powered-features) |
| **Layer** | Backend |
| **Ships with** | [US-704](./US-704-ai-summarise.md) *(backend)*, [US-705](./US-705-ai-suggest-category.md) *(backend)*, [US-706](./US-706-ai-draft-reply.md) *(backend)*, [US-707](./US-707-ai-suggest-solution.md) *(backend)* |
| **Actor** | System |
| **Priority** | P2 |
| **Sprint** | [15 — AI assist](../delivery-plan.md#sprint-15-ai-assist) · Slice S7 |
| **Estimate** | 5 points |
| **Status** | `not started` |
| **BRD requirements** | FR-7.1 |
| **Spec criteria** | AC-702 |
| **Depends on** | [US-701](./US-701-ai-provider-config.md) |

## Story

**As a system**, **I want** AI capabilities behind a service interface, **so that** the underlying provider can be swapped without changing consuming code.

## Business rules

- No BRD BR-n covers this directly. The AI service interface defines methods for summarise, suggest-category, draft-reply, and suggest-solution.

## Acceptance criteria

#### AC1 — IAiService interface (spec AC-702)

Given the backend project, when `IAiService` is defined in the Application layer, then it declares methods for all AI capabilities without depending on any provider SDK.

#### AC2 — Provider implementation (spec AC-702)

Given a concrete AI provider (e.g. OpenAI), when the implementation is registered in DI, then it implements `IAiService` and lives in the Infrastructure layer.

#### AC3 — DI registration (spec AC-702)

Given the application starts, when AI services are registered, then `IAiService` resolves to the configured provider implementation.

## SQL tables

None — this is a code architecture story.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-702 | Unit | `AiServiceInterfaceMethodsExist` | Given `IAiService`, when inspected, then it declares SummariseAsync, SuggestCategoryAsync, DraftReplyAsync, SuggestSolutionAsync. | All four methods present |
| TC-02 | AC-702 | Integration | `AiServiceResolvesFromDi` | Given the DI container, when `IAiService` is resolved, then a non-null implementation is returned. | Valid provider resolved |

## Notes

`IAiService` lives in `CustomerSupport.Application`. Implementations live in `CustomerSupport.Infrastructure`. This keeps the dependency rule intact — Application never references Infrastructure.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
