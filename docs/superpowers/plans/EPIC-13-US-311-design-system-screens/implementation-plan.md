> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier � this plan did not precede its implementation.

# Design application — shell and screens (`admin-app`/`portal-app`) Implementation Plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan
> did not precede its implementation.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the Command Center visual language to every existing screen, using the shared
`common` primitives from the sibling plan (`EPIC-13-US-311-design-system-common`) — restyle only, zero
behaviour change.

**Architecture:** Every task edits an existing component's template; none adds a route, a signal,
or an HTTP call. Reference screens: `stitch_smart_support_ticketing_crm/agent_dashboard_overview/`
and `customer_360_history/` — open the mockup PNGs, not just the HTML (several of the set's PNGs
are broken placeholders; the two named here are intact).

**Tech Stack:** Angular 20 standalone components, Tailwind v4 logical utilities.

**Spec:** [`../../specs/EPIC-13-US-311-command-center-design-application.md`](../../specs/EPIC-13-US-311-command-center-design-application.md)
**Criteria:** `AC-86`, `AC-87`, `AC-88`, `AC-91`, `AC-92`

## Global Constraints — these fail the build, not just review

- **Logical utilities only.** Every physical-direction class (`ml-`→`ms-`, `pr-`→`pe-`,
  `text-right`→`text-end`, `border-r`→`border-e`, `left-`→`start-`) must be translated —
  `rtl-safety.spec.ts` scans every `.html` and fails the build on one.
- **Every user-facing string through `| t`**, both languages in `translations.ts` —
  `no-hardcoded-strings.spec.ts` scans every `.html`.
- **Behaviour is frozen.** This is a restyle. No signal, HTTP call, route, or state transition
  changes. Every existing test passes **unedited** — a failure means the restyle broke something,
  not that the test needs updating.
- `data-testid` attributes are load-bearing (`customer-summary`, `status-action`, `assign-action`,
  `history-timeline`) — keep them exactly.
- **Do not touch `frontend/projects/common/**`** — the sibling plan owns it; consume its component
  API (`CsIcon`, `CsCard`, `CsBadge`) as given.

---

### Task 1: Fonts (`AC-87`)

**Files:**
- Modify: `frontend/projects/admin-app/src/index.html`, `frontend/projects/portal-app/src/index.html`

- [ ] **Step 1: Add Material Symbols beside the existing Google Fonts link**

```html
<link rel="stylesheet"
  href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:wght,FILL@100..700,0..1&display=swap" />
```

- [ ] **Step 2: Commit**

```bash
git add frontend/projects/admin-app/src/index.html frontend/projects/portal-app/src/index.html
git commit -m "feat(design-system): load Material Symbols font (AC-87)"
```

---

### Task 2: The shell (`AC-86`, `AC-92`)

**Files:**
- Modify: `frontend/projects/admin-app/src/app/layout/shell.component.html`

**Interfaces:**
- Consumes: `NAV_ITEMS` (already exists in `shell.component.ts`), `CsIcon` from `common`.

- [ ] **Step 1: Sidebar structure**

```html
<nav class="flex w-sidebar shrink-0 flex-col gap-1 border-e border-border-subtle bg-surface-low px-4 py-6">
  <div class="mb-6 flex items-center gap-3 px-2">
    <span class="grid size-10 shrink-0 place-items-center rounded-lg bg-primary text-on-primary">
      <cs-icon name="support_agent" [size]="24" />
    </span>
    <span class="min-w-0 truncate font-display text-headline-md text-primary">{{ 'app.name' | t }}</span>
  </div>

  @for (item of nav(); track item.path) {
    <div
      class="relative flex items-center gap-2 rounded-lg px-3 py-2 text-label-lg text-on-surface-variant transition-all duration-200 hover:bg-surface-highest"
      routerLinkActive="bg-secondary-container text-on-secondary-container hover:bg-secondary-container"
      #active="routerLinkActive"
    >
      <cs-icon [name]="item.icon" [filled]="active.isActive" />
      <a [routerLink]="item.path" class="min-w-0 truncate before:absolute before:inset-0 before:content-['']">
        {{ item.key | t }}
      </a>
    </div>
  }

  <div class="mt-auto border-t border-border-subtle pt-6">
    @if (session.displayName(); as name) {
      <div class="flex items-center gap-3 px-2">
        <span class="grid size-9 shrink-0 place-items-center rounded-full bg-surface-highest text-on-surface">
          <cs-icon name="person" />
        </span>
        <span class="min-w-0 truncate text-body-sm text-on-surface-variant">{{ name }}</span>
      </div>
    }
  </div>
</nav>
```

**`AC-92`, do not add**: the mockups' global search box, notification bell, "Pulse AI Assistant"
button, or Knowledge Base/Reports nav entries that go nowhere — `nav()` renders `NAV_ITEMS` and
nothing else.

- [ ] **Step 2: Topbar**

```html
<header class="flex h-topbar shrink-0 items-center justify-end gap-4 border-b border-border-subtle bg-surface-lowest px-6">
  <div class="flex items-center gap-3">
    <cs-language-switcher />
    <cs-button variant="ghost" (pressed)="signOut()">{{ 'auth.signOut' | t }}</cs-button>
  </div>
</header>
```

No page title in the topbar — every routed screen renders its own `<h1>`; the route-derived
`title()` signal (already in `shell.component.ts`) sets only the browser tab.

- [ ] **Step 3: Write the nav-route consistency test (`AC-92`)**

```ts
// frontend/projects/admin-app/src/app/layout/shell.component.spec.ts (add to existing suite)
it('AC92: every nav item resolves to a route declared in app.routes.ts', () => {
  for (const item of NAV_ITEMS) {
    const declared = findRouteConfig(routes, item.path); // walks the Routes tree
    expect(declared).toBeTruthy();
  }
});
```

- [ ] **Step 4: Run existing shell tests unedited**

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/shell.component.spec.ts'`
Expected: PASS, all tests including the new one.

- [ ] **Step 5: Commit**

```bash
git add frontend/projects/admin-app/src/app/layout/shell.component.html frontend/projects/admin-app/src/app/layout/shell.component.spec.ts
git commit -m "feat(design-system): Command Center shell restyle (AC-86, AC-92)"
```

---

### Task 3: Page headers, every screen (`AC-87`)

- [ ] **Step 1: Replace every bare heading with the standard pattern**

```html
<header class="mb-6">
  <h1 class="font-display text-headline-lg text-on-surface">{{ 'x.title' | t }}</h1>
  <p class="text-body-md text-on-surface-variant">{{ 'x.subtitle' | t }}</p>
</header>
```

New subtitle keys added to `translations.ts`, both languages, per screen (`dashboard.subtitle`,
`tickets.queue.subtitle`, `customers.subtitle`, `users.subtitle`, etc. — already present in the
live dictionary as of 2026-08-27, confirmed).

- [ ] **Step 2: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/
git commit -m "feat(design-system): standard page headers across screens (AC-87)"
```

---

### Task 4: Wrap screen content in `cs-card` (`AC-87`)

- [ ] **Step 1:** `ticket-queue` · `ticket-create` · `ticket-detail` (three cards: summary, actions,
  history) · `customer-list` · `customer-create` · `customer-detail` (profile + notes + attachments
  cards) · `dashboard` · `users` — each screen's content wrapped in `<cs-card>` per the shape in
  `docs/superpowers/plans/EPIC-13-US-311-design-system-common/implementation-plan.md` Task 3.

- [ ] **Step 2: Run every existing admin-app test unedited**

Run: `cd frontend && npx ng test admin-app --watch=false`
Expected: green, unedited — a `cs-card` wrapper changes markup structure, not selectors any test
depends on (confirmed no test in this suite queries by DOM position).

- [ ] **Step 3: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/
git commit -m "feat(design-system): wrap screen content in cs-card (AC-87)"
```

---

### Task 5: Tables → 12-column grid (`AC-88`)

**Files:**
- Modify: `ticket-queue.component.html`, `customer-list.component.html`

- [ ] **Step 1: Replace `<table>` with the grid row pattern**

```html
<div class="grid grid-cols-12 gap-4 px-4 py-2 border-b border-border-subtle bg-surface-bright text-label-md text-on-surface-variant">
  …
</div>

<a class="grid grid-cols-12 gap-4 px-4 py-3 border-b border-border-subtle hover:bg-surface-bright transition-colors group" [routerLink]="['/tickets', t.id]">
  <span class="col-span-2 font-mono text-data-mono text-on-surface-variant">{{ t.reference }}</span>
  <span class="col-span-5 flex flex-col">
    <span class="text-label-lg text-on-surface truncate group-hover:text-primary">{{ t.subject }}</span>
    <span class="text-body-sm text-on-surface-variant truncate">{{ t.customerName }}</span>
  </span>
  <span class="col-span-3 flex items-center gap-2">
    <cs-badge kind="status" [value]="t.status" />
    <cs-badge kind="priority" [value]="t.priority" />
  </span>
  <span class="col-span-2 text-end text-body-sm text-on-surface-variant">{{ t.createdAt | csDate }}</span>
</a>
```

The whole row is the link — the mockup's row is a click target, and a link on only part of it is a
smaller target than the design shows. `text-end`, never `text-right` (RTL guard).

- [ ] **Step 2: Run existing queue/list tests unedited**

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/ticket-queue.component.spec.ts' --include='**/customer-list.component.spec.ts'`
Expected: PASS, unedited.

- [ ] **Step 3: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.html frontend/projects/admin-app/src/app/features/customers/customer-list.component.html
git commit -m "feat(design-system): 12-column grid tables (AC-88)"
```

---

### Task 6: Badges everywhere status/priority appears (`AC-89`)

- [ ] **Step 1:** Replace every bare `{{ ticket.status }}`/`{{ ticket.priority }}` text with
  `<cs-badge kind="status"/priority" [value]="…" />` — queue rows, ticket detail, dashboard status
  counts. Values stay untranslated (server-owned domain identifiers).

- [ ] **Step 2: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/
git commit -m "feat(design-system): cs-badge everywhere status/priority renders (AC-89)"
```

---

### Task 7: Dashboard stat tiles (`AC-87`)

**Files:**
- Modify: `frontend/projects/admin-app/src/app/features/dashboard/dashboard.component.html`

- [ ] **Step 1: Implement**

```html
<div class="bg-surface-lowest rounded-xl border border-border-subtle shadow-card p-4">
  <div class="flex items-center justify-between">
    <span class="text-label-md text-on-surface-variant">{{ 'dashboard.new' | t }}</span>
    <cs-icon name="inbox" />
  </div>
  <p class="font-display text-display text-on-surface mt-2">{{ counts().new }}</p>
</div>
```

Keep the four independent `AsyncState` signals in `dashboard.component.ts` unchanged — a failing
count must not blank the other three tiles.

- [ ] **Step 2: Run existing dashboard test unedited**

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/dashboard.component.spec.ts'`
Expected: PASS, unedited.

- [ ] **Step 3: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/dashboard/dashboard.component.html
git commit -m "feat(design-system): dashboard stat tiles (AC-87)"
```

---

### Task 8: Customer detail two-column layout

**Files:**
- Modify: `frontend/projects/admin-app/src/app/features/customers/customer-detail.component.html`

- [ ] **Step 1: Implement**

```html
<div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
  <cs-card [heading]="'field.customer' | t" class="lg:col-span-1"> … profile, cs-icon mail/phone … </cs-card>
  <div class="lg:col-span-2 flex flex-col gap-6">
    <admin-customer-notes [customerId]="id()" data-testid="customer-notes" />
    <admin-customer-attachments [customerId]="id()" data-testid="customer-attachments" />
  </div>
</div>
```

Notes render as a vertical timeline: a bordered line with a coloured marker per entry, author and
timestamp on each card.

- [ ] **Step 2: Run existing customer-detail test unedited**

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/customer-detail.component.spec.ts'`
Expected: PASS, unedited — `data-testid` attributes preserved exactly.

- [ ] **Step 3: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/customers/customer-detail.component.html
git commit -m "feat(design-system): customer detail two-column layout (AC-87)"
```

---

### Task 9: Login restyle only

**Constraint:** change no behaviour — `AC-55`/`AC-56` and the `returnUrl` test depend on it.

- [ ] **Step 1:** Centre a `cs-card` on `bg-surface` with the brand block above it.
- [ ] **Step 2: Run existing login test unedited**

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/login.component.spec.ts'`
Expected: PASS, unedited.

- [ ] **Step 3: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/auth/login.component.html
git commit -m "style(design-system): restyle login screen, no behaviour change"
```

## Definition of done

`ng test common` and `ng test admin-app` green, output pasted · `ng build admin-app` clean · task
record in `docs/superpowers/plans/EPIC-13-US-311-design-system-screens/README.md` naming which screens
were restyled and which were not, if any were left. **Do not commit without being asked** — treat
every "Commit" step above as `git add` only, per this session's standing instruction.

