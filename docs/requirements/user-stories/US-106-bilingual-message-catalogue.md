# US-106 · Messages are bilingual and loaded once at startup

| Field | Value |
|---|---|
| **Story** | `US-106` *(was `US-1.06`)* |
| **Epic** | [EPIC-12 Platform features](../epics/EPIC-12-platform.md) |
| **Feature** | [`FEAT-01` Platform foundation](../delivery-plan.md#feat-01--platform-foundation) |
| **Layer** | Backend |
| **Ships with** | — Enabler. No user-facing surface, so nothing pairs with it. |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Internal — Product owner |
| **Priority** | P0 |
| **Sprint** | [1 — Foundation and authentication](../delivery-plan.md#sprint-1--foundation-and-authentication) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `superseded` |
| **BRD requirements** | FR-12.1, FR-12.2, NFR-11 |
| **Spec criteria** | FND-14, FND-15, FND-16, FND-17, FND-20 |
| **Depends on** | — |

## Story

**As a product owner**, **I want** every system message to exist in Arabic and English before the application will start, **so that** a missing translation is a startup failure rather than a customer seeing a blank string.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- Codes are prefixed `ERR` for failures, `CON` for confirmations, `VAL` for validation (from FND-14).
- The catalogue is one flat file keyed by domain key, each entry carrying `ar` and `en` (from FND-15).
- It is parsed once at startup into an immutable structure, not per request (from FND-16).
- Malformed content, or a duplicate key, fails at startup with a message naming the file and key
  (from FND-17).
- Placeholder substitution uses named tokens; an unmatched token stays visible rather than producing
  a blank (from FND-20).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Codes use documented prefixes (spec FND-14)

Given system codes, when they are classified, then failures are prefixed `ERR`, confirmations `CON`,
validation `VAL`.

#### AC2 — Flat bilingual catalogue file (spec FND-15)

Given the message catalogue, when examined, then it is one flat file keyed by domain key, each entry
carrying `ar` and `en`.

#### AC3 — Parsed once at startup (spec FND-16)

Given application start-up, when the catalogue loads, then it is parsed once into an immutable
structure, not per request.

#### AC4 — Bad catalogue fails at startup (spec FND-17)

Given malformed content or a duplicate key, when the catalogue loads, then start-up fails with a
message naming the file and key.

#### AC5 — Unmatched token stays visible (spec FND-20)

Given named placeholder tokens, when substitution lacks an argument, then the token stays visible
rather than producing a blank.

## SQL tables

None — the catalogue is a file (`Resources.yml`), not persisted data.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | FND-15 | Application.Tests | ✅ `YamlMessageCatalogTests.Resolves_Both_Languages_For_A_Key` | a catalogue entry / resolve / inspect | both `ar` and `en` returned |
| TC-02 | FND-20 | Application.Tests | ✅ `Substitutes_Named_Placeholders` | entry with `{max}` / format with an argument / inspect | token substituted |
| TC-03 | FND-20 | Application.Tests | ✅ `Missing_Placeholder_Argument_Leaves_The_Token_Visible` | format with the argument missing / inspect | literal `{max}` visible, no throw |
| TC-04 | FND-17 | Application.Tests | ✅ `Malformed_Yaml_Fails_At_Load_Naming_The_File` | malformed YAML / load at startup / observe | startup failure naming file and key |
| TC-05 | FND-17 | Application.Tests | ✅ `Entry_Missing_A_Language_Fails_At_Load` + `Entry_With_Blank_Text_Fails_At_Load` + `Missing_File_Fails_With_The_Path` | incomplete or absent catalogue / load / observe | startup failure with the offending path |
| TC-06 | FND-16 | Application.Tests | ✅ `Unknown_Key_Returns_The_Key_Rather_Than_Throwing` (immutability in use) | resolve an unknown key / inspect | key returned, no mutation, no throw |
| TC-07 | FND-14 | Application.Tests | ✅ `SystemCodeMapTests.Codes_Use_The_Documented_Prefixes` | every code constant / reflect on prefixes / — | `ERR`/`CON`/`VAL` prefixes hold |
| TC-08 | FND-16 | Api.IntegrationTests | `planned` — singleton lifetime asserted at wiring level | composed API / resolve `IMessageCatalog` twice / compare | same instance; parsed once, not per request |

## Notes

Failing at startup is the whole design. A catalogue that degrades gracefully to a blank string ships a blank string to a customer, and nobody notices until they do. The visible-token rule follows the same reasoning: `{max}` rendered literally is diagnosable, an empty gap is not.

## Open questions

None.

## Status evidence

**Superseded 2026-08-25.** The code that satisfied this story was replaced when the CCE Platform
reference was adopted as the CRM baseline ([ADR-0009](../../adr/0009-adopt-the-support-platform-as-the-crm-baseline.md)).

The criterion it cites is still a valid **requirement**. What is no longer true is that this
codebase meets it: the implementation named in the previous evidence is archived, not running. The
adopted platform may satisfy the same intent by different means, but that has **not been
re-verified**, and carrying a `done` for code that no longer exists would be the exact false claim
this file exists to prevent.

Re-verify against the new baseline, or re-scope the story to the platform equivalent.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
