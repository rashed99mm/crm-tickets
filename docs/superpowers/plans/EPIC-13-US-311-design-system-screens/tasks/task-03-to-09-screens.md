# Tasks 3–9 — Page headers, cards, tables, badges, tiles, customer 360, login

| Field | Value |
|---|---|
| Plan | [`../implementation-plan.md`](../implementation-plan.md) |
| Feature | Command Center design application — shell and screens |
| Criteria | `AC-87`, `AC-88`, `AC-89`, `AC-91` |
| Status | `done`, with two recorded gaps (page subtitles, visual verification) |
| Commit | _not committed_ |

Kept as one record because the nine screens were restyled as a single pass over the same component
language — splitting it into seven files would repeat the same four paragraphs seven times.

## Files

Templates rewritten:

- `features/tickets/ticket-queue.component.html` · `ticket-create.component.html` ·
  `ticket-detail.component.html`
- `features/customers/customer-list.component.html` · `customer-create.component.html` ·
  `customer-detail.component.html` · `customer-notes.component.html` ·
  `customer-attachments.component.html`
- `features/dashboard/dashboard.component.html`
- `features/users/users.component.html`
- `features/account/change-password.component.html`
- `features/auth/login.component.html`
- `features/errors/forbidden.component.html`

Component files touched **for their `imports` array only** (`CsCard`, `CsIcon`, `CsBadge`), plus one
addition noted below. No signal, HTTP call, route or state transition was changed anywhere.

Deleted: `features/users/users.component.css`, `features/account/change-password.component.css` —
both were hand-rolled `.panel` / `.table` / `.pill` styling that `cs-card` and the token utilities
now do. Their `styleUrl` entries went with them.

## What was done, by criterion

**`AC-87` — every content surface is a card.** All thirteen templates above are built from
`cs-card`. The card's body is unpadded by design, so table rows run flush to the edge and supply
their own `px-4` while forms wrap themselves in `p-6`.

**`AC-88` — tables.** `ticket-queue` and `customer-list` became the mockups' twelve-column grid:
a `label-md` header row on `bg-surface-bright`, rows with `border-b` and a `hover:bg-surface-bright`
tint, identifiers in `font-mono text-data-mono`, a two-line primary cell (subject over customer;
name over email) and an end-aligned `<time>`. **The whole row is the anchor**, as the mockups'
`cursor-pointer group` row is a click target — a link on the reference alone is a much smaller
target than the design shows.

`dashboard`'s "my work" list and `users`' staff list stayed real `<table>` elements. Two reasons,
and both matter: neither row is a click target, so the semantic table is the right element; and
`AC77: renders the rows in the order the server returned them` and the staff-list test read
`tbody tr`. They got the same header tint, borders, mono references and two-line cell.

**`AC-89` — badges.** `cs-badge` renders status and priority in the queue rows, the dashboard's
work list, the ticket detail header, and the dashboard's metric tiles. No bare `{{ ticket.status }}`
or `{{ ticket.priority }}` survives anywhere — verified by grep, not by reading. Values stay
untranslated; `MVP-13` recorded why server-owned identifiers do.

**`AC-91` — logical utilities only.** Every physical utility in the mockups was translated through
the spec's table. `rtl-safety.spec.ts` is green.

**`T8` — customer 360.** `customer-detail` is `grid lg:grid-cols-3`: the profile card
(`lg:col-span-1`) with a circular avatar mark and `mail` / `phone` / `event` contact rows, and the
notes and attachments cards in `lg:col-span-2`. Notes render as the mockup's timeline — a
continuous `border-s` rule, an `edit_note` marker per entry, each note a bordered card on
`surface-bright` carrying its author and time. Ticket history got the same treatment.

**`T9` — login.** Centred `cs-card` on `bg-surface` with the brand mark above it. Behaviour
untouched: `AC-55`/`AC-56` and the `returnUrl` test pass unedited.

## Deviations and gaps

1. **No page subtitles. This is the one part of `T3` that is not done.** Every screen got the
   `<h1 class="font-display text-headline-lg">` page header; none got the `<p class="text-body-md
   text-on-surface-variant">` subtitle beneath it. Subtitles need new dictionary keys, and
   `translations.ts` lives in `common/`, which this plan does not own — a key that does not exist is
   a TypeScript error, not a missing string. The keys needed are listed below and the markup is one
   line per screen once they land.

2. **The ticket queue and customer list borrow `customers.detail.recorded` for their date column
   header.** It renders correctly in both languages ("Recorded" / "تاريخ التسجيل") but it is a
   customer-scoped key doing general work. `field.created` is requested below.

3. **`users`' active/deactivated chip is not a `cs-badge`.** That component maps server-owned ticket
   vocabulary onto the status and priority tokens; "Active" is neither, so it would land on the
   unknown-value fallback and render grey in both states — which is the one thing the chip exists to
   distinguish. It is a hand-built chip on `bg-success/12` and `bg-error/12` with the same shape.

4. **One non-template line was added to a component.** `dashboard.component.ts` gained a
   `tileIcon: Partial<Record<TicketStatus, string>>` lookup so each metric tile carries the mockups'
   glyph. It is presentation, has a fallback, and changes nothing about which statuses are counted.

5. **`attachments`' download and remove error paragraphs moved** from below the list into the card's
   upper block, beside the upload errors. Same `data-testid`s, same conditions; they now sit with
   the other messages rather than floating between the picker and the file list.

6. **Visual verification against the mockups was NOT performed.** The spec asks for it by
   screenshot. It needs a running backend and a signed-in session, which was out of reach here. The
   restyle is verified by the suite, the guards and the generated CSS — not by eye. **This is an
   open item, not a completed one.**

## i18n keys still needed (both languages)

| Key | en | ar |
|---|---|---|
| `field.created` | Created | تاريخ الإنشاء |
| `dashboard.subtitle` | Here is what is happening with your queues today | إليك ما يجري في قوائمك اليوم |
| `tickets.queue.subtitle` | Every ticket you can act on, newest first | كل التذاكر التي يمكنك التعامل معها، الأحدث أولاً |
| `customers.subtitle` | Everyone who has contacted support | كل من تواصل مع الدعم |
| `users.subtitle` | Who can sign in, and what they may do | من يمكنه تسجيل الدخول، وما المسموح له به |
| `password.subtitle` | Choose a new password for your own account | اختر كلمة مرور جديدة لحسابك |

## Test evidence

Run, not assumed:

```
ng test admin-app --watch=false   Test Files 17 passed (17)   Tests 118 passed (118)
ng test common   --watch=false    Test Files 23 passed (23)   Tests 100 passed (100)
ng build admin-app                Application bundle generation complete. [3.278 seconds]
ng build portal-app               Application bundle generation complete. [7.105 seconds]
```

Every pre-existing test passed **unedited**. The two added tests are both in
`layout/nav-routes.spec.ts`.

## The one that nearly got through

`rtl-safety.spec.ts` scans comments, not only attributes. A comment in `ticket-detail` explaining
*why* the timeline uses `border-s` named the physical utility it was avoiding, and failed the build
on the string it was warning about. Worth knowing before writing the next such comment.
