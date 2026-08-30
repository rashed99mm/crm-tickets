---
name: sdd-workflow
description: Use when turning a brief or requirement into a spec, writing acceptance criteria, breaking work into tasks, or whenever asked to build a feature before a spec exists - defines the spec format, AC numbering and traceability this project is graded on
---

# Spec-driven workflow

## Overview

Every feature here travels brief → spec → plan → test → code. The artifacts are the deliverable,
not paperwork around it: the assessment grades whether a clear spec with acceptance criteria
existed *before* implementation, and git timestamps are the proof.

**The core discipline:** an acceptance criterion you cannot write a failing test for is not a
criterion, it is a wish. If you cannot express it as Given/When/Then, the requirement is still
ambiguous — go back and resolve the ambiguity rather than writing something vague enough to
agree with later.

## Spec format

Specs live in `docs/superpowers/specs/YYYY-MM-DD-<topic>-design.md`.

```markdown
# <Feature>

## Problem
What the user cannot do today, in their terms. No solution language.

## Assumptions
Numbered. Each is a question you did not get to ask — write it so it can be proven wrong.
A1. <assumption>

## Out of scope
What this deliberately does not do. Prevents a boundary being read as an omission.

## Acceptance criteria
AC-1. Given <state>, when <action>, then <observable outcome>.
AC-2. ...
Include negative paths. A spec with only happy paths is half a spec, and the
"Testing, Security & Edge Cases" criterion is scored on the other half.

## Design
Architecture, data model, API surface, error behaviour. Scale to the complexity.
```

### Writing acceptance criteria

`AC-n` ids are **stable and permanent**. Tasks reference them, tests reference them, the
traceability table references them. Renumbering breaks every reference — append new criteria
rather than inserting.

| Bad | Why | Better |
|---|---|---|
| "Validation works correctly" | Untestable. What input, what outcome? | "Given a title over 200 chars, when creating an event, then 400 with a `title` field error" |
| "The API is fast" | No threshold | "Given 1000 events, when listing page 1, then a response in under 300ms" |
| "Handles errors gracefully" | Which errors, what behaviour? | "Given the database is unreachable, when listing events, then 503 and no stack trace in the body" |

## Task breakdown

Plans live in `docs/superpowers/plans/YYYY-MM-DD-<feature>/implementation-plan.md`.

- Every task names the `AC-n` it satisfies. A task satisfying none is scope creep — cut it or
  add the missing criterion to the spec.
- Every `AC-n` is covered by at least one task. Uncovered criteria are the most common way a
  feature ships incomplete while looking finished.
- Order tasks by dependency, and inside a slice: failing test → implementation → refactor.
- Size a task so it is one commit. If it needs "and" to describe, split it.
- Task code is grounded in the real files it touches — cited paths and line numbers, not
  descriptions (see "Tasks are execution plans, not descriptions" below).

## Tasks are execution plans, not descriptions

A task that says "implement the WhatsApp sender" is not a task, it's a title. Every task has to give
whoever executes it enough to start typing, not enough to start researching:

- **Ground it in the actual codebase before writing it.** Read the real file(s) the task extends or
  mirrors — the existing sender, the existing handler, the existing controller — and cite exact
  paths and line numbers, not "similar to the existing pattern." A citation the next person can
  re-check (`EmailNotificationChannelSender.cs:39-83`) is worth more than a paragraph describing what
  that file roughly does.
- **Write real code in the task, not prose about code.** A signature
  (`Task<ChannelSendResult> SendAsync(...)`) is not a task; the method body is, or a close diff of
  one. If the task's code can't be pasted into the target file with only names changed, it isn't
  finished being planned.
- **Surface what reading the real code changes about the plan.** Grounding a task in the actual
  files routinely finds things a description-only pass misses — a field that's non-nullable when the
  plan assumed otherwise, a validation rule duplicated in two places, a dependency-rule violation the
  new code would introduce. When it does, the finding goes into the plan *and* the spec, not just
  into whoever found it. (Example: `2026-08-27-feat-24-communication-channels`'s plan was hardened
  by reading `Customer.cs`, `TicketMessage.cs` and `ServiceCollectionExtensions.cs` directly, which
  surfaced three real gaps a prose plan had missed — `Customer.Email` is non-nullable with no
  channel-sourced email to give it, the message `Channel` allow-list is duplicated in the domain
  entity *and* its validator, and the well-known-actor helper the new handler needs lives in a layer
  `Application` cannot reference.)
- **Point at examples the codebase already proves work.** A new provider adapter cites the sibling
  adapter it copies; a new hub cites the existing hub's mapping and the exact line that constrains
  the new one. A task with no pointer to a real precedent invites reinventing a convention that
  already exists three files away.

This applies even to a plan-only deliverable — spec + plan + tasks, no code executed yet. It still
needs real code *inside the plan*, or it is a design document wearing a task list's clothes.

## The feature loop — backend, frontend, tests, ship

**A feature ships as backend + frontend + tests, together, or it has not shipped.** Do not organise
work into a backend phase followed by a frontend phase. The unit of delivery is a vertical feature
(`FEAT-nn` in `docs/requirements/delivery-plan.md`): the API, the screen that consumes it, and the
tests for both.

```
spec (approved)
   ↓
backend plan   →  backend implementation (TDD)
   ↓
frontend plan  →  frontend implementation (TDD)
   ↓
tests green at every level the feature touches
   ↓
ship: feature-complete commit
   ↓
next feature
```

**The rule that matters: when a backend plan's tasks are complete, the very next artifact you produce
is the frontend plan for the same feature.** Not the next feature's backend plan. Write it with
`superpowers:writing-plans`, over the frontend `AC-n` the feature's stories cite, then implement it.

A story's **Ships with** row names its counterpart. If a backend story has one, that story is not
done until its counterpart is done.

### Why

Layering hides integration risk until the end. An envelope shape, an error contract or a field name
the frontend cannot actually consume gets discovered sprints after it was decided, when changing it
is most expensive. Shipping vertically moves that discovery into the feature that caused it — which
is why `FEAT-04` (ticket capture) is the highest-value early feature: its form is the first thing
that proves the validation contract is consumable at all.

### Definition of shipped

All six, or it is not shipped:

1. Backend `AC-n` implemented, each covered by a test naming it.
2. Frontend `AC-n` implemented, each covered by a test naming it.
3. Unit, integration and component tests green — **run, with output pasted**, not assumed.
4. Clean build under warnings-as-errors.
5. The story files' **Status** and **Status evidence** updated from what was actually executed.
6. A commit whose message states the criteria it implements.

The single Playwright journey (`AC-64`) is **terminal, not per feature** — the spec defines exactly
one. Per-feature end-to-end tests would mean amending an approved spec; do not add them without that
amendment.

### Exceptions, which must be recorded not assumed

Some features legitimately have no counterpart layer: infrastructure with no user surface, a backend
capability whose UI the spec places inside another feature's screen, or cross-cutting frontend
behaviour whose server half already shipped. **Record which and why in the delivery plan.** An
unrecorded missing layer is indistinguishable from a forgotten one.

If a backend feature has no frontend criteria in the spec **and should have**, that is a spec gap —
raise it, do not invent criteria to fill it.

## Task records are kept, always

**Every feature gets one folder**, `docs/superpowers/plans/YYYY-MM-DD-<feature>/`, holding the plan,
the record, and one markdown file per task:

```
docs/superpowers/plans/
  2026-08-25-feat-02-authentication/
    implementation-plan.md                      the plan - intent
    README.md                                   the record - index, criteria delivered, gaps accepted
    tasks/
      task-01-<slug>.md
      task-02-<slug>.md
```

**Restructured 2026-08-26.** These previously sat as a sibling `<feature>.md` beside a `<feature>/`
folder, with task records loose inside it. One folder per feature keeps a feature's intent, outcome
and per-task detail together, and stops the plan and its record drifting apart in a directory
listing that had grown to seventeen plans.

Each task record carries: the criteria it covers, its commit hash, the **test evidence actually
observed**, and every **deviation from the plan** with the reason. Write it as the task completes,
not at the end from memory.

A plan states intent; the records state outcome, and the gap between them is where the engineering
happened. That gap is the only durable answer to "why does this code not look like the plan?" - and
on a graded project it is also the difference between claiming a task was done and showing it.

Never delete a task record to tidy up. A record of a task that went wrong is worth more than one
that reads as though nothing did.

## Traceability chain

```
brief.md  →  AC-n in spec  →  task in plan  →  test name citing AC-n  →  commit
```

Name tests after the criterion (`Create_Rejects_Title_Over_200_Chars` for AC-4, or an explicit
`[Trait("AC", "4")]`). This is what lets you answer "show me where requirement 4 is tested"
instantly instead of searching.

## The gate

**No implementation code before an approved spec.** Not a draft spec — an approved one.

The pressure to skip this is strongest on small tasks, and skipping it there is exactly what
the first rubric criterion is designed to catch. If a change genuinely seems too small to
specify, say so and ask; do not decide alone.

## Red flags

| Thought | Reality |
|---|---|
| "The requirement is obvious, I'll just build it" | Obvious to you now. Write the AC — if it is genuinely obvious that costs one line. |
| "I'll write the spec after, to match what I built" | That is a transcript, not a spec, and the timestamps show it. |
| "I'll number the ACs later" | Tasks and tests need the ids to reference. Number them now. |
| "Happy path is enough for the spec" | Half the testing criterion is negative paths. |
| "This assumption is safe" | Then it costs nothing to write down. Unwritten assumptions become wrong requirements. |
| "Backend's done, I'll do all the screens later" | That is the layered plan this project replaced. Write the frontend plan now, for this feature. |
| "The endpoint works, so the feature is done" | An endpoint no screen consumes has shipped nothing. Check the story's **Ships with** row. |
| "I'll wire the UI once every endpoint exists" | Then every contract mistake surfaces at once, at the point it is most expensive to fix. |
| "Tests pass" (not run) | Run them and paste the output. Those are different sentences and only one is evidence. |
| "No frontend criteria exist, so it's backend-only" | Maybe — or the spec has a gap. Decide which, and record it. Never invent criteria to fill it. |
| "I described what the task does" | A description isn't a task. Read the real file it extends and write the actual code, cited by path and line. |
