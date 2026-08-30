# Applying the Command Center design

**Date:** 2026-08-26
**Source:** `stitch_smart_support_ticketing_crm/` — the `command_center/DESIGN.md` system and its four
intact screens: `agent_dashboard_overview`, `customer_360_history`,
`ai_ticket_management_workspace`, `management_analytics_sla_performance`
**Story:** cross-cutting. Applies to every screen the MVP shipped.

## Problem

The design **tokens** were extracted in Phase 2 and live in `common/src/styles/theme.css`. The
**design** was not. Every screen since has been assembled from plain utility classes: bare tables
with a bottom border, status rendered as text, no cards, no icons, no elevation, a sidebar that is a
list of links.

The product currently looks like scaffolding that happens to use the right colours.

## What "the same exact design" can and cannot mean

Three things make a literal copy impossible, and each is a decision rather than a shortcut.

### 1. The mockups are RTL-unsafe. **144 physical-direction utilities.**

```
37 × text-right   18 × left-0   15 × border-r   11 × text-left
 7 × pr-4          7 × ml-64     6 × border-l    5 × pl-10   …
```

This build **fails on every one of them**: `rtl-safety.spec.ts` scans every template and has already
caught one. `MVP-13` shipped Arabic with `dir="rtl"`, and the design's own document asks for it
("ensure visual parity between English (LTR) and Arabic (RTL)").

**Decision: translate, do not transcribe.** Every physical utility becomes its logical equivalent —
`ml-3`→`ms-3`, `pr-4`→`pe-4`, `text-right`→`text-end`, `border-r`→`border-e`, `left-0`→`start-0`.
The rendered result in English is pixel-identical; in Arabic it mirrors instead of breaking.

### 2. The mockups use twelve tokens the theme does not define

`surface-container-lowest` · `surface-container-low` · `surface-container-high` ·
`surface-container-highest` · `surface-bright` · `background` · `on-background` ·
`secondary-container` · `on-secondary-container` · `surface-50` · `label-lg` · `data-mono`

The Phase 2 extraction renamed some (`surface-container-lowest` → `surface-lowest`) and dropped
others. **Decision: extend the theme with the missing roles under the existing naming convention,
and map the mockups' names onto ours** rather than renaming what fifteen templates already use.

### 3. The mockups show features this product does not have

Global search, notification bell, "Pulse AI Assistant", Knowledge Base and Reports nav, tasks and
activity feeds, chat composers, SLA charts, account-health panels, customer avatars.

**Decision: apply the chrome and the component language; do not invent the features.** A nav item
that goes nowhere and a search box that searches nothing are worse than their absence — they are
lies about what the product does. This is the line the design application must not cross.

## Assumptions

- **A21.** Material Symbols Outlined is loaded from Google Fonts, as every mockup does. It is the
  one external asset the design depends on; without icons the sidebar and cards are unrecognisable.
- **A22.** Where a mockup's document and its code disagree, **the code wins** — the same rule the
  Phase 2 token extraction used.
- **A23.** Screens the MVP does not have (knowledge base, reports, analytics) are not built. Their
  mockups inform component styling only.

## Out of scope

New features · dark mode (the mockups carry `dark:` variants; nothing else in this product does) ·
charts · avatars/photography · animations beyond the mockups' `transition-colors` and `active:scale-95`.

## Acceptance criteria

Appended; nothing renumbered.

- **AC-86** (P0) The shell renders the Command Center chrome: a 280px sidebar on
  `surface-low` with a branded logo block, icon+label nav items, an **indigo-filled active item**,
  and a bottom-anchored group; a 64px white topbar with a bottom border. Only routes that exist
  appear.
- **AC-87** (P0) Every content surface is a **card** — `surface-lowest`, `rounded-xl`, `1px
  border-subtle`, the mockups' `0 4px 12px rgba(0,0,0,0.02)` shadow — with a bordered header strip
  carrying the title and any action.
- **AC-88** (P0) Data tables follow the mockup: a `label-md` header row on a tinted background, rows
  with a bottom border and a hover tint, **identifiers in `data-mono`**, a two-line primary cell
  (title over secondary text), and end-aligned numerics.
- **AC-89** (P0) Ticket **status** and **priority** render as badges using the semantic tokens
  already in the theme — never as plain text. Priority carries the mockup's dot indicator.
- **AC-90** (P0) Buttons follow the three documented variants — primary (filled `primary`),
  secondary (white, `1px` border), ghost — and inputs use the documented border-plus-focus-ring.
- **AC-91** (P0) **No physical-direction utility is introduced.** `rtl-safety.spec.ts` stays green,
  and the Arabic layout mirrors.
- **AC-92** (P1) No control is added for a feature that does not exist. Every nav item, button and
  link resolves to a real route or action.

## Design

### Token additions — `common/src/styles/theme.css`

| Add | Value | Why |
|---|---|---|
| `--color-surface-bright` | `#f8f9ff` | the mockups' hover/strip tint, distinct from `surface` |
| `--color-secondary-container` | `#645efb` | the **active nav pill** |
| `--color-on-secondary-container` | `#ffffff` | its text |
| `--text-label-lg` (+ line-height) | `0.8125rem` / `1rem`, 600 | nav items, table row titles |
| `--text-data-mono` (+ line-height) | `0.8125rem` / `1.25rem` | ticket references, ids |
| `--shadow-card` | `0 4px 12px rgba(0,0,0,0.02)` | Layer 1 lift |
| `--shadow-popover` | `0 12px 24px rgba(0,0,0,0.08)` | Layer 2 |

Existing names are **not** renamed. `surface-container-lowest` in a mockup maps to our
`surface-lowest`; `surface-container-low` → `surface-low`; `surface-container-highest` →
`surface-highest`; `background` → `surface`; `surface-50` → `surface-bright`.

### The physical → logical translation table

Normative. Every mockup class passes through it:

| Mockup | Use |
|---|---|
| `ml-*` / `mr-*` | `ms-*` / `me-*` |
| `pl-*` / `pr-*` | `ps-*` / `pe-*` |
| `text-left` / `text-right` | `text-start` / `text-end` |
| `border-l` / `border-r` | `border-s` / `border-e` |
| `left-*` / `right-*` | `start-*` / `end-*` |
| `rounded-l*` / `rounded-r*` | `rounded-s*` / `rounded-e*` |

### Component language

Extracted verbatim from `agent_dashboard_overview/code.html`, with the translation applied:

```html
<!-- Card -->
<div class="bg-surface-lowest rounded-xl border border-border-subtle shadow-card
            flex flex-col overflow-hidden">
  <div class="px-4 py-2 border-b border-border-subtle bg-surface-bright
              flex justify-between items-center">
    <h2 class="font-display text-headline-md text-on-surface flex items-center gap-2">…</h2>
  </div>
  …
</div>

<!-- Table header / row -->
<div class="grid grid-cols-12 gap-4 px-4 py-2 border-b border-border-subtle
            bg-surface-bright text-label-md text-on-surface-variant">…</div>
<div class="grid grid-cols-12 gap-4 px-4 py-3 border-b border-border-subtle
            hover:bg-surface-bright transition-colors cursor-pointer group">…</div>

<!-- Status badge -->
<span class="inline-flex items-center px-2 py-0.5 rounded text-label-md
             bg-status-open text-on-primary">Open</span>

<!-- Priority badge, with the dot -->
<span class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-label-md
             bg-priority-urgent/10 text-priority-urgent border border-priority-urgent/20">
  <span class="size-1.5 rounded-full bg-priority-urgent"></span> Urgent
</span>

<!-- Active nav item -->
<a class="flex items-center gap-2 py-2 px-3 rounded-lg bg-secondary-container
          text-on-secondary-container text-label-lg transition-all duration-200">…</a>
```

### Icons

`Material Symbols Outlined`, loaded in each app's `index.html` beside the existing Google Fonts
link. A `<cs-icon name="dashboard" [filled]="true">` wrapper in `common/ui` keeps the
`font-variation-settings` incantation in one place rather than in fifteen templates.

## Testing

The visual result is not unit-testable, and pretending otherwise would produce assertions that lock
in markup without proving anything. What **is** tested:

| Test | Criterion |
|---|---|
| `rtl-safety.spec.ts` — already exists, must stay green | `AC-91` |
| `no-hardcoded-strings.spec.ts` — already exists, must stay green | — |
| `cs-badge` renders the status token class for each of the five statuses | `AC-89` |
| `cs-badge` renders the dot for a priority | `AC-89` |
| Every existing component test still passes | all |
| `AC92: every nav item resolves to a declared route` | `AC-92` |

`AC-92`'s test is the one worth having: it reads the shell's nav table and asserts each `path`
matches a route in `app.routes.ts`. It is what stops the design's decorative nav items being copied
in.

**Verification is visual**, by screenshot against the mockups, and is recorded as such.
