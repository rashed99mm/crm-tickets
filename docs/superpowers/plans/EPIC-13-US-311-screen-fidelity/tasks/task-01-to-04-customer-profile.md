# Tasks 1–4 — `cs-placeholder` and the customer profile workspace

| Field | Value |
|---|---|
| Plan | [`../implementation-plan.md`](../implementation-plan.md) |
| Spec | [`../../../specs/EPIC-13-US-311-screen-fidelity-design.md`](../../../specs/EPIC-13-US-311-screen-fidelity-design.md) |
| Criteria | `AC-93`, `AC-94`, `AC-95`, `AC-96`, `AC-97`, `AC-99`, `AC-100` |
| Status | `done`, with `T2` dropped and two gaps recorded |
| Commit | _not committed_ |

## Files

New:

- `common/src/lib/ui/placeholder.component.ts` · `.html` · `.spec.ts` (4 tests)
- `common/src/lib/ui/initials.ts` · `.spec.ts` (6 tests)

Rewritten:

- `admin-app/features/customers/customer-detail.component.html` — identity band + 3 / 6 / 3
- `admin-app/features/customers/customer-notes.component.html` — the mockup's 48px timeline
- `admin-app/features/customers/customer-attachments.component.html` — the mockup's file rail

Touched for a display derivation or an `imports` entry only:
`customer-detail.component.ts` (`initials`), `customer-list.component.ts` (`initials`),
`customer-attachments.component.ts` (`sizeAndDate`, `glyph`, `glyphTone`),
`common/src/public-api.ts`, `common/src/lib/i18n/translations.ts` (30 keys, both languages).

No signal, HTTP call, route or state transition was changed anywhere (`AC-100`).

## What was done, by criterion

**`AC-93` — the identity band and the 3 / 6 / 3.** A full-bleed band (`-mx-6 -mt-6`, cancelling the
shell's inset) carrying a `size-20` initials mark with a neutral presence dot, the name with a
neutral `verified` glyph, the identity line, a plan chip, and the end-aligned **New ticket / Edit /
Remove** group. Below it the mockup's twelve-column `3 / 6 / 3` inside `max-w-7xl mx-auto`,
collapsing to one column below `lg` with the context rail first.

**`AC-94` — the start rail.** Contact Info with the mockup's icon + role + value rows (email as a
`mailto`, its verification line, phone as a `tel`, WhatsApp, Company HQ) and Account Details as a
hairline-ruled label/value list ending in the Tags group.

**`AC-95` — the centre feed.** The notes card, restyled to the mockup's timeline: an absolute rule
inset 8 units from each end so it stops short of the first and last marker, `size-12` bordered
markers with a `ring-4` cutout, and each entry's body inset in its own panel. Plus the ticket lane's
explicit *not available* line.

**`AC-96` — the end rail.** Files & Attachments with a per-content-type glyph (`picture_as_pdf`,
`image`, `description`, falling back to `attach_file`), a mono `size • date` line, and the mockup's
dashed upload target at the foot of the card.

**`AC-97` — placeholders.** Eleven positions: identity line, plan chip, email verification,
WhatsApp, Company HQ, account manager, MRR, timezone, tags, and — on other screens — assignee and
ticket opener. A twelfth reuses it for an absent optional phone.

## Deviations and dropped work

1. **`T2` (`mergeActivity`) was dropped, and the segmented filter header with it.** Reading
   `ticket.api.ts` first showed `TicketFilters` has no `customerId`, so the queue endpoint cannot
   answer "this customer's tickets" — leaving notes as the only lane. A merge over one source is
   dead code and three tabs over one populated lane are controls for capabilities the product does
   not have, which `AC-92` forbids. **The spec was amended before the template was written**, not
   after. Gap `G-7`.

2. **"Quick Notes" is at the head of the centre feed, not in the end rail.** Splitting
   `CustomerNotesComponent` across two columns would break six passing tests reading the composer
   and the list from one fixture, and `AC-100` freezes behaviour; a second component re-fetching
   notes would give the screen two sources of truth for one list. Recorded in the spec.

3. **`data-testid="customer-profile"` moved from one card to the whole start rail.** `AC-71`'s test
   reads email, phone and the recorded date from that one region, and the mockup's composition
   splits them across two cards. **This was caught by the test failing, not by review** — the fix
   went into the template, never the test.

4. **The mockup's `more_vert` overflow menu became the existing Remove button.** A menu holding one
   item is a menu for a feature set that does not exist.

5. **The presence dot and verification glyph are neutral, never green or blue.** Neither is
   recorded, and a green dot is an assertion rather than a decoration.

6. **The upload input became `sr-only` inside a label, not `hidden`.** A hidden input is unreachable
   by keyboard and by a screen reader, which would make the only control on that card inoperable for
   anyone not using a mouse.

## Test evidence

Run, not assumed:

```
ng test common     --watch=false   Test Files 26 passed (26)   Tests 115 passed (115)
ng test admin-app  --watch=false   Test Files 17 passed (17)   Tests 119 passed (119)
ng build admin-app                 Application bundle generation complete. [3.534 seconds]
ng build portal-app                Application bundle generation complete. [2.558 seconds]
```

All 119 pre-existing `admin-app` tests pass **unedited**. `common` went 109 → 115 with the ten new
`cs-placeholder` and `initialsOf` tests, less the pre-existing count.

The generated stylesheet was checked for the three classes most at risk of being scanned away, the
failure mode `cs-badge` documents — styled under `ng serve`, colourless in production:

```
.rtl\:rotate-180:where(:dir(rtl),[dir=rtl],[dir=rtl] *){rotate:180deg}
.even\:bg-surface-low:nth-child(2n){background-color:var(--color-surface-low)}
.bg-primary\/5{background-color:color-mix(in srgb,#00288e 5%,transparent)}
```

## Not done

**Visual verification against the mockups was NOT performed.** The spec asks for it by screenshot;
it needs a running backend and a signed-in session. The composition is verified by the suite, the
guards and the generated CSS — **not by eye. This is an open item.**

## The one worth knowing about

`no-hardcoded-strings` scans visible text between tags, so the mockup's `1.2 MB • Oct 12` could not
be built by putting a bullet in the template — the separator is visible text and the guard is built
to catch exactly that. It became a parameterised dictionary entry (`attachments.meta`), which is
also the only form a translator can move the separator in.
