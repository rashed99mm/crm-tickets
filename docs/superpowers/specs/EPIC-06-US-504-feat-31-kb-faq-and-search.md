# FEAT-31 — KB FAQ & search (mockup fidelity)

**Date:** 2026-08-28
**Status:** Approved
**Type:** Frontend-only vertical over two screens
**Epic:** `EPIC-13` (mockup fidelity), `EPIC-06` (knowledge base)
**Source:** `stitch_smart_support_ticketing_crm/knowledge_base_faq_search/`,
`stitch_smart_support_ticketing_crm/knowledge_base_management/`

## Problem

Two knowledge-base surfaces in the running app do not yet reproduce the supplied Stitch
reference screens:

- The portal `kb-list` (`portal-app/src/app/features/kb/kb-list.component.html`) renders a
  flat list, with no hero, no category grid, no FAQ bento, no "Still need help" footer.
- The admin `kb-admin` (`admin-app/src/app/features/kb/kb-admin.component.html`) renders
  a form-on-top-of-list layout, with no category bento, no "Recent Articles" table with
  Title/Category/Author/Updated/Visibility columns, and no "KB Insights" sidebar with
  Total Views, Most Viewed Articles, Searches With No Results and the "Content Strategy"
  tip.

A reviewer comparing the running app with `code.html`/`screen.png` finds the chrome,
the section order, the typography and the palette are all inconsistent with the
reference. The data the screens already fetch is sufficient; what is missing is the
layout and token fidelity of the reference composition.

## Assumptions

- **A1.** The supplied `code.html` files are the authoritative source for structure,
  typography, spacing, colour and component states. `screen.png` is used for visual
  comparison.
- **A2.** Tailwind v4 remains the styling engine. Logical utilities only — no
  `left-*`/`right-*`/`pl-*`/`pr-*` for horizontal padding/margin, no `[dir="rtl"]`
  branching in CSS.
- **A3.** Two design systems stay distinct. The portal `kb-list` uses Command Center
  tokens (blue primary `#00288e`). The admin `kb-admin` uses Proton Precision tokens
  (black primary `#000000`). They are differentiated by a `data-design-system`
  attribute on the routed container, scoped via the common theme.
- **A4.** The shared shell (`PortalPublicShell`/`AdminShell`) is the runtime chrome.
  The mockup's topbars and sidebars are reference only.
- **A5.** No new backend endpoint is added. The existing `ContentsApi.list` /
  `ContentsApi.faq` / `KbAdminApi.list` / `KbAdminApi.categories` / `KbAdminApi.versions`
  cover the data each screen needs. Designed regions without backing data render the
  translated `unavailable` placeholder and disabled controls.
- **A6.** No fabricated customer, analytics, AI, avatar or "view count" data is
  presented as real. The hero image of the support team and per-user avatar placeholders
  are not loaded — initials on a coloured circle stand in, as `audit-log` already does.
- **A7.** Responsive behaviour is defined at 375px, 768px, 1280px and 1920px per the
  mockup-fidelity design.

## Out of scope

- New backend entities, endpoints, migrations or permissions.
- Replacing Angular standalone components, signals or the shared library architecture.
- The "Create / edit article" form inside the admin KB screen (it already exists and
  ships; this feature only changes the listing chrome around it).
- KB search ranking, full-text indexing, or no-result search-log telemetry.

## User stories

- `US-509` KB admin list
- `US-510` KB admin create
- `US-511` KB admin edit
- `US-512` KB admin publish
- `US-513` Portal KB browse

## Acceptance criteria

### Portal KB list (Command Center)

- **AC-500.** Given a customer navigates to `/kb`, when the page renders, then a hero
  section with `How can we help you today?` heading, the four category cards
  (Getting Started / Account Management / Billing & Invoices / Technical Support) and a
  FAQ bento (one featured FAQ plus two smaller FAQs) match the composition in
  `knowledge_base_faq_search/code.html` lines 201–293.
- **AC-501.** Given the page renders, when the search box is used, then pressing Enter
  and clicking the "Search" button both call `ContentsApi.list(term)` and update the
  listed articles section.
- **AC-502.** Given the API is loading, empty or fails, when the screen renders, then
  loading, empty and error states are visible in their designed positions and the
  surrounding hero / categories / FAQ sections remain unchanged.
- **AC-503.** Given the user changes locale to Arabic, when the page renders, then
  layout, navigation, icons, text alignment and spacing mirror correctly without
  physical-direction utility classes.
- **AC-504.** Given a customer submits the contact form, when the click handler runs,
  then a translated toast appears and the form is reset — no new API is added.
- **AC-505.** Given the article list returns zero results, when the screen renders,
  then the search section shows the translated "no results" message and the hero,
  categories, FAQ and "Still need help" sections remain visible and unchanged.

### Admin KB management (Proton Precision)

- **AC-510.** Given a ContentManager navigates to `/kb-admin`, when the page renders,
  then a top header with title "Knowledge Base", subtitle, and a "Create New Article"
  button match the composition in `knowledge_base_management/code.html` lines 254–267.
- **AC-511.** Given the page renders, when the bento category grid is shown, then three
  category cards (Onboarding / Troubleshooting / Billing) each show an icon, title,
  short description and an article-count label.
- **AC-512.** Given the page renders, when the "Recent Articles" table is shown, then
  the columns `Title / Category / Author / Last Updated / Visibility` and four seeded
  rows are present, with a "View All Articles" link below the table.
- **AC-513.** Given the API has returned zero articles, when the page renders, then
  the table renders its header row and an `unavailable` body row, and the bento
  category cards remain visible.
- **AC-514.** Given the page renders, when the "KB Insights" sidebar is shown, then
  the `Total Views (30d)` progress bar, the `Most Viewed Articles` list, the
  `Searches With No Results` tag cloud and the `Content Strategy` tip are all
  rendered in their designed positions. Each region without backing data shows
  `unavailable` in the translated dictionary string.
- **AC-515.** Given the user changes locale to Arabic, when the page renders, then
  layout, navigation, icons, text alignment and spacing mirror correctly.
- **AC-516.** Given the routed container renders, when the DOM is inspected, then the
  root element has `data-design-system="proton"` and the Command Center routes
  continue to carry `data-design-system="command-center"`.

### Cross-cutting

- **AC-520.** Given the user changes locale to Arabic, when the page renders, then
  no `left-*`/`right-*`/`pl-*`/`pr-*` utility classes are present on the rendered
  templates (logical-only contract inherited from the mockup-fidelity spec).
- **AC-521.** Given the existing component specs, when the changes ship, then the
  pre-existing tests (kb-list, kb-admin, common design tokens) remain green.
- **AC-522.** Given the frontend builds, when `npx ng build admin-app` and
  `npx ng build portal-app` are run, then both complete without warnings-as-errors
  or missing style assets.

## Design

### File map

```
docs/superpowers/specs/EPIC-06-US-504-feat-31-kb-faq-and-search-design.md   (this file)
docs/superpowers/plans/EPIC-06-US-504-feat-31-kb-faq-and-search/
  implementation-plan.md
frontend/projects/portal-app/src/app/features/kb/
  kb-list.component.ts
  kb-list.component.html
  kb-list.component.spec.ts
frontend/projects/portal-app/src/app/features/home/
  home.component.html
frontend/projects/admin-app/src/app/features/kb/
  kb-admin.component.ts
  kb-admin.component.html
  kb-admin.component.spec.ts
frontend/projects/common/src/styles/theme.css   (extend with Proton tokens)
frontend/e2e/mockup-fidelity.spec.ts             (one new spec)
```

### Tokens

The Proton Precision palette is added to `frontend/projects/common/src/styles/theme.css`
under a `[data-design-system="proton"]` scope, so `AC-516` is mechanical. The portal
`kb-list` continues to use the existing Command Center tokens (`#00288e` primary,
`#f8f9ff` background, `surface-container-lowest` etc.).

### Component composition

The portal `kb-list` is a single page with five sections: hero, categories, FAQ bento,
search-results list, "Still need help" CTA. The existing `CsCard` / `CsEmptyState` /
`CsErrorState` / `CsLoadingState` shared components are reused for the search-results
list, the FAQ cards, and the category cards.

The admin `kb-admin` keeps its existing create/edit form (US-510/US-511) but moves it
into a side panel that the "Create New Article" button opens — so the list/bento/insight
layout is always visible, matching the mockup.

## Verification

Run from `frontend/`:

```
npx ng test common --watch=false
npx ng test admin-app --watch=false
npx ng test portal-app --watch=false
npx ng build admin-app
npx ng build portal-app
npx playwright test mockup-fidelity
```

Each criterion above has at least one named test in the corresponding `.spec.ts`.
A criterion without a test is not "shipped".
