# Tasks 6–10 — the remaining screens

| Field | Value |
|---|---|
| Plan | [`../implementation-plan.md`](../implementation-plan.md) |
| Spec | [`../../../specs/EPIC-13-US-311-screen-fidelity-design.md`](../../../specs/EPIC-13-US-311-screen-fidelity-design.md) |
| Criteria | `AC-98`, `AC-99`, `AC-100` |
| Status | `done` for all six screens; visual verification **not** performed |
| Commit | _not committed_ |

Kept as one record because the six screens were composed against the same mockup vocabulary in a
single pass; six files would repeat the same three paragraphs six times.

## What each screen got

### `tickets` — `ticket_queue` (`T6`)

The mockup's **six columns**, replacing four: `ID(2) · Subject(3) · Category(2) · Priority(1) ·
Status(2) · Assignee(2)`. Header row upper-cased and tracked on `surface-low`. Alternating stripes.
Assignee is the mockup's own italic *Unassigned* when the ticket is unheld, and the placeholder when
it is held — `TicketListItem` carries `assigneeId` and no name (gap `G-8`), and a bare guid is data
an agent cannot use. Footer replaced with the mockup's summary-plus-chevrons pair.

The `Created` column was dropped, because the mockup has no such column and six columns were already
the width budget. The date is on the detail screen's meta strip.

### `tickets/:id` — `ticket_detail_chatbot` (`T7`)

A full-bleed **header band**: the reference as a boxed mono chip, the priority badge beside it, the
subject, an *Opened by* byline read off the `Created` history entry, then the mockup's ruled **meta
strip** — Status · Assignee · Category · Opened. The history timeline's 8px dots became the mockup's
`size-8` bordered markers carrying a glyph per change type (`add_circle`, `person_add`, `swap_horiz`,
`sync_alt`, `restart_alt`), which is what lets an agent find the reopen in twenty status changes.

**The mockup's reply box and its whole AI assistant rail were not built.** There is no comment
endpoint and no assistant; a composer that posts nowhere is the exact `AC-92` violation. The existing
customer-summary and actions cards keep the rail.

### `tickets/new` — `submit_ticket` (`T8`)

Centred `max-w-3xl`, a back-to-the-queue control beside the title, an intro block naming the card,
the mockup's field order (subject → the two-up selects → description), and its end-aligned
**Cancel / Submit** pair above a rule, Submit carrying the `send` glyph.

**The mockup's attachment dropzone was not built** — tickets have no attachment endpoint; customers
do, which is why the dashed target exists on the customer screen and not here.

### `dashboard` — `agent_dashboard_overview` (`T9`)

`max-w-7xl` canvas and the mockup's **8 / 4** split: metric tiles and the work list in the wide
column, the supervisor's unassigned counter in the rail. Tiles gained the mockup's quarter-circle
corner wash — `pointer-events-none` and `aria-hidden`, positioned with logical insets so it moves
corner in Arabic.

**The mockup's rail also holds SLA charts and an activity feed; neither exists (`A23`).** The rail
carries the one real panel that belongs there. An empty rail is honest; an invented one is not.

### `customers` and `users` — the mockups' table language (`T10`)

Upper-cased tracked header on `surface-low`, alternating stripes, hover a step darker than the
stripe. The customer rows lead with an initials mark, as the mockups lead with a photograph.
`customers` also got the chevron footer.

## The `nth-child` trap, and how it was caught

`even:bg-surface-low` tinted the **first** data row in the two grid tables, which reads as a
selected row rather than a stripe: those rows are anchors sitting after the header row inside the
same container, so `nth-child` counts the header as child 1. Fixed to `odd:` there; the two real
`<table>` screens keep `even:`, because `tbody` restarts the count and the same parity comes out.

Found by reading the emitted selector in the built stylesheet, not by eye — which is also the check
that proved the class was emitted at all.

## Deviations from the plan

The plan's `T6` said the queue's table "is already the mockup's". It was not: it had four columns to
the mockup's six, and no stripes. The column set was rebuilt rather than adjusted.

## Test evidence

```
ng test common     --watch=false   Test Files 26 passed (26)   Tests 115 passed (115)
ng test admin-app  --watch=false   Test Files 17 passed (17)   Tests 119 passed (119)
ng build admin-app                 Application bundle generation complete. [3.534 seconds]  no warnings
ng build portal-app                Application bundle generation complete. [2.558 seconds]
```

Every pre-existing test passes **unedited**.

## Screens rebuilt, and screens not

| Screen | Mockup | Done |
|---|---|---|
| `customers/:id` | `customer_profile_history` | yes |
| `tickets` | `ticket_queue` | yes |
| `tickets/:id` | `ticket_detail_chatbot` | yes, minus reply box and AI rail |
| `tickets/new` | `submit_ticket` | yes, minus the attachment dropzone |
| `dashboard` | `agent_dashboard_overview` | yes, minus SLA and activity panels |
| `customers` | `admin_ticket_management` table language | yes |
| `users` | `admin_dashboard` table language | yes |
| `login` · `change-password` · `forbidden` · `customers/new` | — | **unchanged.** No mockup governs them; the previous pass already carded them |
| shell chrome | `agent_dashboard_overview` | **unchanged, deliberately.** The mockups' search box, notification bell and Support link are controls for capabilities this product lacks (`AC-92`), and the sign-out placement is fixed by `AC-63`'s test |

**Visual verification against the mockups was NOT performed** — same reason as the customer profile
record. It is the open item for this increment.
