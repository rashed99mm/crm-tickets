# US-510 · KB Admin: Create Article Form

| Field | Value |
|---|---|
| **Story** | `US-510` |
| **Epic** | [EPIC-06 Knowledge Base](../epics/EPIC-06-knowledge-base.md) |
| **Feature** | [`FEAT-11` Knowledge base](../delivery-plan.md#feat-11--knowledge-base) |
| **Layer** | Frontend |
| **Ships with** | [US-503](./US-503-category-tag-management.md) *(backend)* |
| **Actor** | KB Author |
| **Priority** | P0 |
| **Sprint** | [11 — Knowledge base](../delivery-plan.md#sprint-11-knowledge-base) · Slice S4 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-6.1, FR-6.2 |
| **Spec criteria** | AC-6.1, AC-6.2 |
| **Depends on** | [US-503](./US-503-category-tag-management.md) |

## Story

**As a KB author**, **I want** to create articles with title, body, category, and tags, **so that** new knowledge is added to the system.

## Business rules

- No BRD BR-n covers this directly. Title is required and must not exceed 500 characters.
- No BRD BR-n covers this directly. Body is required and supports rich text / Markdown.
- No BRD BR-n covers this directly. Category is required; tags are optional.

## Acceptance criteria

#### AC1 — Create form fields (spec AC-6.1)

Given the create article form, when loaded, then fields for title, body, category (required), and tags (optional) are displayed.

#### AC2 — Submit creates article

Given valid form data, when the author submits, then a new article is created in Draft status.

#### AC3 — Validation errors shown

Given invalid form data (missing title, missing body, missing category), when submitted, then validation errors are displayed inline.

#### AC4 — Category selection from existing

Given the create form, when the category field is focused, then existing categories are shown for selection.

#### AC5 — Tag input supports multiple

Given the create form, when the author types a tag and presses Enter, then the tag is added to the list.

## SQL tables

None — frontend story. Creates records in existing `KnowledgeArticles`, `Categories`, and `ArticleTags` via backend API.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-6.1 | Unit | `CreateForm_DisplaysAllFields` | Given create form, when rendered, then title, body, category, tags fields visible | All fields visible |
| TC-02 | AC-6.1 | Unit | `CreateForm_CategoryIsRequired` | Given create form, when submitted without category, then validation error shown | Error displayed |
| TC-03 | AC-6.1 | Unit | `CreateForm_TitleIsRequired` | Given create form, when submitted without title, then validation error shown | Error displayed |
| TC-04 | AC-6.1 | Unit | `CreateForm_BodyIsRequired` | Given create form, when submitted without body, then validation error shown | Error displayed |
| TC-05 | AC-6.1 | Unit | `CreateForm_SubmitWithValidData_CreatesArticle` | Given valid form data, when submitted, then article created in Draft | Article exists |
| TC-06 | AC-6.2 | Unit | `CreateForm_CategoryDropdown_ShowsExisting` | Given 3 categories exist, when category focused, then 3 options shown | 3 options |
| TC-07 | AC-6.2 | Unit | `CreateForm_TagInput_AddsTagOnEnter` | Given tag input, when type and Enter, then tag added to list | Tag in list |
| TC-08 | AC-6.1 | E2E | `CreateArticle_FullFlow` | Given user on create form, when fill and submit, then article created and listed | Article in list |

## Notes

- Category dropdown fetches from GET /api/kb/categories.
- Tags are entered as free text; existing tags are reused if slug matches.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
