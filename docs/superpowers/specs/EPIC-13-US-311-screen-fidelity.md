# Screen-for-screen fidelity to the stitch mockups

**Date:** 2026-08-26
**Source:** `stitch_smart_support_ticketing_crm/` — all thirteen screens, not only the four
Command Center ones
**Supersedes nothing.** Extends
[`EPIC-13-US-311-command-center-design-application.md`](./EPIC-13-US-311-command-center-design-application.md)
(`AC-86`…`AC-92`), which stays in force.
**Story:** cross-cutting. Applies to every screen the MVP shipped.

## Problem

The first design pass applied the Command Center **component language** — cards, badges, the
twelve-column table grid, the sidebar pill — and it landed. What it did not do is match any single
mockup's **screen composition**. Every screen is now one card, or a stack of cards, in a single
column. The mockups are not: each one is a specific multi-column workspace with a header band, rails
and a centre feed.

The clearest case is the customer screen. `customer_profile_history` is a full-width identity band
above a **3 / 6 / 3** workspace — context rail, activity timeline, actions rail. What shipped is a
`lg:grid-cols-3` with the profile in one column and two stacked cards in the other two. The
component vocabulary is right; the layout is not the design.

## Which mockup governs which screen

The mockup set contains two design systems (recorded in
[`EPIC-01-US-101-frontend-foundation-design.md`](./EPIC-01-US-101-frontend-foundation-design.md)):
**Command Center** (`primary: #00288e`, four screens) and **Proton Precision**
(`primary: #000000`, nine screens). The screens this product needs are split across both.

| Screen | Governing mockup | System |
|---|---|---|
| `customers/:id` | `customer_profile_history` (composition) + `customer_360_history` (component detail) | Proton / CC |
| `tickets` | `ticket_queue` | Proton |
| `tickets/:id` | `ticket_detail_chatbot` | Proton |
| `tickets/new` | `submit_ticket` | Proton |
| `dashboard` | `agent_dashboard_overview` | CC |
| `customers` | `admin_ticket_management`'s table language | Proton |
| `users` | `admin_dashboard`'s table language | Proton |
| shell chrome | `agent_dashboard_overview` — unchanged, see below | CC |

### The palette does not change. **This is the one deliberate departure from "exact".**

`theme.css` is Command Center: `primary #00288e`, semantic status and priority colour. Proton
Precision's primary is `#000000` and its own screens render status and priority as undifferentiated
grey — it never implements the semantic colour its design document promises.

**Decision: take composition and structure from the governing mockup, and colour from the theme.**
Adopting Proton's black primary would mean either two palettes in one product — incoherent — or
discarding the status/priority colour that is the single thing a ticket queue exists to convey. The
foundation spec already chose Command Center on exactly that reasoning and an ADR is not reopened
for a restyle. Where a Proton screen paints a chip grey, this build paints it with the semantic
token, and every `bg-primary` in a Proton mockup resolves to `#00288e`.

This is stated here rather than discovered later: a reviewer comparing a rendered screen against
`ticket_queue/code.html` will find the layout identical and the accent blue instead of black, and
that difference is a decision with a reason.

## Assumptions

- **A24.** The `screen.png` files for `ticket_queue`, `admin_dashboard`, `customer_profile_history`
  and `knowledge_base_management` are 28-byte placeholders, not images — verified, not assumed.
  For those four screens **`code.html` is the only source**, so composition is read from the markup.
- **A25.** Mockup content is illustrative. "Sarah Jenkins, VP of Operations at TechCorp" describes
  the *shape* of an identity line, not data this product holds.
- **A26.** The mockups' `space-y-sm space-y-md` and `bg-surface-container-lowest bg-surface-bright`
  duplicate-class pairs are generator artefacts. The **last** class wins in CSS, so the last is the
  intent.

## The gap between the design and the data

`customer_profile_history` shows fifteen fields. The backend's `CustomerDto` has five:
`id`, `name`, `email`, `phone`, `createdAt`.

| Mockup field | Backed? |
|---|---|
| name · email · phone · created | **yes** |
| notes · attachments · tickets | **yes**, through their own endpoints |
| job title · company · company HQ · account id · account manager · MRR · timezone · tags | no |
| avatar photograph · verified flag · online presence · WhatsApp · email threads | no |

**Decision: render the designed position, mark the absence.** A card or row the mockup shows is
rendered, and a field with nothing behind it shows an explicit *not recorded* state carrying a
comment that names the gap. Three alternatives were rejected:

- **Invent values.** Fabricated data in a graded deliverable. Not negotiable.
- **Omit the cards.** Loses the composition, which is the whole point of this increment, and hides
  a real product gap that is better made visible.
- **Extend the backend.** Correct eventually, and a multi-feature job through the full gate —
  schema, migration, endpoints, validation. Out of scope here, and recorded as `G-6`.

**This does not weaken `AC-92`.** A placeholder is a read-only label reading *not recorded*; it
promises nothing. `AC-92` forbids adding a **control** — a button, link or nav item — for a
capability the product lacks, and no placeholder is a control. The distinction is normative: a
disabled "Upgrade Plan" button would violate `AC-92`; a `Plan — not recorded` row does not.

## Out of scope

New backend fields · new features · dark mode · charts · avatar photography · the mockups' AI
assistant panels, global search, notification bell, knowledge base and reports · the Proton palette.

## Acceptance criteria

Appended; nothing renumbered.

- **AC-93** (P0) `customers/:id` renders `customer_profile_history`'s composition: a full-width
  identity band — avatar mark with presence dot, name with verification glyph, secondary identity
  line, attribute chips, and an end-aligned action group — above a twelve-column **3 / 6 / 3**
  workspace that collapses to one column below `lg`, context rail first.
- **AC-94** (P0) The start rail carries **Contact Info** and **Account Details**. Contact rows are
  the mockup's icon + `label-md` role + value, with a verification line under the email. Account
  Details is a label/value list separated by hairlines and ends with the **Tags** group.
- **AC-95** (P0) The centre column is an **activity feed**: a compose-in-place head, then the
  mockup's timeline — a continuous vertical rule, a circular marker per entry, and a bordered card
  carrying actor, end-aligned timestamp and body. Where a lane the mockup shows has no reachable
  data source, the feed says so in that lane's position rather than omitting it.
- **AC-96** (P0) The end rail carries **Files & Attachments** with a per-content-type glyph, a
  `size • date` mono line, and the mockup's dashed upload target.
- **AC-97** (P0) Every mockup field with no backing data renders in its designed position as an
  explicit *not recorded* placeholder. No invented value appears anywhere, and no placeholder is
  interactive.
- **AC-98** (P0) `tickets`, `tickets/:id`, `tickets/new`, `dashboard`, `customers` and `users` each
  match their governing mockup's composition from the table above — column structure, header band,
  rails and feed — not merely its component vocabulary.
- **AC-99** (P0) No physical-direction utility is introduced. `rtl-safety.spec.ts` and
  `no-hardcoded-strings.spec.ts` stay green, and every pre-existing test passes **unedited**.
- **AC-100** (P0) Behaviour is unchanged. No component's signals, HTTP calls, routing or state
  transitions are altered by this increment; `data-testid` hooks are preserved.

## Design

### `customers/:id` — the composition, translated

`customer_profile_history` in logical utilities, with the theme's token names substituted per the
mapping table in the previous spec (`surface-container-lowest` → `surface-lowest`, `background` →
`surface`, `surface-container-high` → `surface-high`, `outline-variant` → `border-subtle`):

```html
<!-- identity band -->
<div class="border-b border-border-subtle bg-surface-bright px-6 py-6">
  <div class="mx-auto flex max-w-7xl flex-col justify-between gap-4 lg:flex-row lg:items-start">
    …avatar + name + chips…            …action group…
  </div>
</div>

<!-- 3 / 6 / 3 -->
<div class="mx-auto grid max-w-7xl grid-cols-1 items-start gap-5 lg:grid-cols-12">
  <div class="flex flex-col gap-5 lg:col-span-3">…context…</div>
  <div class="flex flex-col gap-5 lg:col-span-6">…feed…</div>
  <div class="flex flex-col gap-5 lg:col-span-3">…actions…</div>
</div>
```

`gap-5` is the mockup's `gap-gutter` (20px). `max-w-7xl mx-auto` is the mockup's own container and
is what stops the three rails stretching to absurd widths on an ultrawide monitor.

### The feed: what the mockup asks for, and what is reachable

`customer_profile_history`'s centre column is a **merged** feed — emails, chats and ticket events on
one timeline behind an `All activity | Tickets | Emails` filter — and its end rail carries a separate
**Quick Notes** composer. Reading the code showed neither is reachable as drawn, and both departures
are decisions rather than omissions.

**1. There is no merged feed, because there is only one lane to merge.**

| Mockup lane | This product |
|---|---|
| notes | `GET /api/Customers/{id}/notes` — **reachable** |
| files | `GET /api/Customers/{id}/attachments` — reachable, but owned by the end rail's component |
| tickets | **not reachable.** `TicketFilters` has `status`, `mine` and `unassigned`. There is no `customerId` filter, so the queue endpoint cannot answer "this customer's tickets" |
| emails, chats | no such feature |

A merge function over one source is dead code, and a three-tab filter over one populated lane is
three controls for two capabilities the product does not have — which `AC-92` forbids outright. So
the planned `mergeActivity` and the segmented filter header are **both dropped**, and the ticket
lane renders an explicit *not available on this screen yet* line in its designed position, per
`AC-97`'s rule applied to a lane rather than a field. A `customerId` filter on the queue endpoint is
the fix and it is a backend change with its own gate; it is recorded as `G-7`.

**2. Quick Notes sits at the head of the centre feed, not in the end rail.**

The mockup separates the composer from the timeline by two columns. Here they are the same feature:
a note is this product's only unit of customer activity, so the composer and the feed it writes into
are one card. Splitting them across columns would mean either splitting
`CustomerNotesComponent` — which breaks six passing tests that read the composer and the list from
one fixture, and `AC-100` freezes behaviour — or a second component re-fetching notes, which gives
the screen two sources of truth for one list and a composer whose new note does not appear in the
feed beside it. Neither is worth a column position.

### The timeline entry

One shape. `edit_note` marker on `secondary-container`, a `ring-4 ring-surface-lowest` cutout where
it crosses the rule, and the entry as a bordered card on `surface-bright` carrying author,
end-aligned timestamp and body — the existing template, which was already built to
`customer_360_history`'s timeline and needs no change.

### The placeholder

One component, so the absence looks the same everywhere and grep finds every instance:

```html
<span class="text-body-sm text-on-surface-variant/60 italic">{{ 'field.notRecorded' | t }}</span>
```

Rendered through `cs-placeholder`, whose only job is that markup plus a required `field` input used
as its own documentation. `italic` and the 60% alpha are doing real work: an agent scanning the rail
must be able to tell *absent* from *empty string* at a glance.

### Chips

`Enterprise Plan` and `VIP` in the mockup are unbacked, so the band's chip row renders the
placeholder in the chip position rather than two invented chips. Presence dot and verification glyph
are likewise unbacked: both render in the neutral *unknown* tone with a `title`, never green or blue,
because a green dot is an assertion.

## Testing

Composition is not unit-testable and assertions over layout classes lock in markup while proving
nothing. What is tested:

| Test | Criterion |
|---|---|
| `cs-placeholder` renders the dictionary's not-recorded string, and is not focusable | `AC-97` |
| `rtl-safety.spec.ts` green over the rewritten templates | `AC-99` |
| `no-hardcoded-strings.spec.ts` green over the new headings and chips | `AC-99` |
| every pre-existing `admin-app` and `common` test green, **unedited** | `AC-99`, `AC-100` |

`cs-placeholder`'s not-a-control test is the one worth having. It is the mechanical check that keeps
the `AC-97` / `AC-92` boundary — render the absence, never a control for the missing capability —
true on the sixteenth unbacked field someone adds, not just on the fifteen reviewed here.

**No behaviour is added by this increment**, which is why there is nothing else to test: with the
merge dropped, every screen is markup over signals that already existed.

**Composition verification is visual**, by comparing each rendered screen against its mockup, and is
recorded as such — including, explicitly, if it was not performed.
