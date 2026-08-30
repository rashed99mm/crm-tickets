# FEAT-31 — KB FAQ & search implementation plan

**Spec:** [`../../specs/EPIC-06-US-504-feat-31-kb-faq-and-search-design.md`](../../specs/EPIC-06-US-504-feat-31-kb-faq-and-search-design.md)
**Epic:** `EPIC-13` (mockup fidelity) + `EPIC-06` (knowledge base)
**Layer:** Frontend only, no backend change
**Status:** Planned

## Working rules

- Angular standalone components, signals, `OnPush`, shared `common` components and
  logical Tailwind utilities only.
- No new backend endpoint. `ContentsApi` / `KbAdminApi` already cover the data.
- Failing test first, named after its `AC-n`. Implementation, then re-run the focused
  spec, then the app-wide tests, then the build.
- One logical change per file edit. No unowned CSS, no global tokens leaking from
  screen-specific styles.

## Task sequence

| # | Scope | Criteria | Output |
|---|---|---|---|
| 01 | Add Proton tokens to `theme.css` | `AC-516` | design-system switch |
| 02 | TDD portal `kb-list` hero / categories / FAQ / CTA | `AC-500`, `AC-503`, `AC-505` | portal screen sections |
| 03 | TDD portal `kb-list` search behaviour | `AC-501`, `AC-502`, `AC-504` | search form wiring |
| 04 | TDD admin `kb-admin` header / bento / table | `AC-510`, `AC-511`, `AC-512`, `AC-513`, `AC-516` | admin screen sections |
| 05 | TDD admin `kb-admin` insights sidebar | `AC-514`, `AC-515` | insights regions |
| 06 | E2E and existing-tests regression | `AC-520`, `AC-521`, `AC-522` | full suite |

## Concrete task files

### Task 01 — Proton tokens in common theme

Read: `frontend/projects/common/src/styles/theme.css`,
`frontend/projects/admin-app/src/app/layout/shell.component.html`,
`stitch_smart_support_ticketing_crm/knowledge_base_management/code.html` (lines 12–170).

Edit `theme.css` to add a `[data-design-system="proton"] { ... }` block overriding the
primary, surface, secondary and border tokens with the Proton palette from the mockup.
Document the override contract inline so the admin `kb-admin` (and the future Proton
screens) can opt in by setting the attribute on the routed container.

Test:
- `frontend/projects/common/src/styles/theme.spec.ts` (extend if missing) — assert
  `data-design-system="proton"` resolves `--color-primary` to `#000000` and
  `data-design-system="command-center"` resolves it to `#00288e`.

Run:
```
npx ng test common --watch=false
npx ng build common
```

### Task 02 — Portal KB list: sections

Read: `frontend/projects/portal-app/src/app/features/kb/kb-list.component.ts`,
`frontend/projects/portal-app/src/app/features/kb/kb-list.component.html`,
`stitch_smart_support_ticketing_crm/knowledge_base_faq_search/code.html` (lines 200–315).

Edit `kb-list.component.html` to add the four reference sections above the existing
search-results list:
1. Hero with `How can we help you today?`, search input + "Search" button, popular
   keywords row.
2. "Browse Categories" 4-up grid (Getting Started, Account Management, Billing &
   Invoices, Technical Support). Each card is a router-link with a coloured
   material-symbol icon, title, short description and a "View Articles →" affordance.
3. "Frequently Asked Questions" — one featured card spanning 2 columns plus two
   standard cards. Data source: `ContentsApi.faq()` already loaded in the component.
4. "Still need help?" — a contact card with two buttons.

Keep the existing search-results list as section 5. Add `data-design-system="command-center"`
on the section root (matches the rest of the portal).

Test (write before editing the template):
- `AC-500_kb_list_renders_hero_categories_faq_cta`
- `AC-503_kb_list_rtl_logical_only`
- `AC-505_kb_list_hero_persists_when_results_empty`

Run:
```
npx ng test portal-app --watch=false -- --include='**/kb-list.component.spec.ts'
```

### Task 03 — Portal KB list: search behaviour

The hero search input and the "Search" button both call `load()`. The input emits on
Enter and on a debounced input event (300ms) to match the existing `submitSearch`
behaviour. The button click calls the same `submitSearch()`.

Add the toast/reset behaviour for the contact form (`AC-504`). No backend.

Test:
- `AC-501_kb_list_search_via_enter_and_button`
- `AC-502_kb_list_loading_empty_error_states`
- `AC-504_kb_list_contact_form_resets_with_toast`

### Task 04 — Admin KB management: header / bento / table

Read: `frontend/projects/admin-app/src/app/features/kb/kb-admin.component.ts`,
`frontend/projects/admin-app/src/app/features/kb/kb-admin.component.html`,
`stitch_smart_support_ticketing_crm/knowledge_base_management/code.html` (lines 254–377).

Edit the template so the page root carries `data-design-system="proton"`, the existing
header keeps its title/subtitle/Create button, then renders:
1. 3-up category bento (Onboarding, Troubleshooting, Billing) sourced from
   `categories()`.
2. "Recent Articles" data table with the five columns from the mockup and a row for
   each article in `articles()`. Empty body row labelled `unavailable` when the list is
   empty.
3. "View All Articles" link at the bottom of the table card.

Move the existing create/edit form (US-510/US-511) into a side panel that opens when
`formOpen()` is true. Use a `CsDialog` if it fits; otherwise an absolute-positioned
panel that overlays the page chrome. The form's `versions()` and `selectedCategoryId()`
remain unchanged.

Test:
- `AC-510_kb_admin_renders_header_with_create_button`
- `AC-511_kb_admin_renders_three_category_bento`
- `AC-512_kb_admin_renders_recent_articles_table`
- `AC-513_kb_admin_table_renders_unavailable_when_empty`
- `AC-516_kb_admin_root_has_proton_design_attribute`

### Task 05 — Admin KB management: insights sidebar

Add a 320px sidebar to the right of the workspace that renders, in order:
1. `Total Views (30d)` — stat label + number (use a `CsStatCard` with the value
   `unavailable` until the API returns one).
2. `Most Viewed Articles` — list of three `routerLink` rows, values `unavailable` for
   each until the API supports it.
3. `Searches with No Results` — flex-wrap tag cloud; `unavailable` until the API
   supports it.
4. `Content Strategy` — lightbulb tip card (translated).

Test:
- `AC-514_kb_admin_insights_regions_render_unavailable`
- `AC-515_kb_admin_rtl_logical_only`

### Task 06 — Regression and E2E

Run from `frontend/`:
```
npx ng test common --watch=false
npx ng test admin-app --watch=false
npx ng test portal-app --watch=false
npx ng build admin-app
npx ng build portal-app
npx playwright test mockup-fidelity
```

Extend `frontend/e2e/mockup-fidelity.spec.ts` with one Playwright test per feature
that captures `/kb-admin` at 1280px and asserts the Proton attribute is present, and
one that captures `/kb` and asserts the Command Center attribute and the hero h1.

## File-level execution map

| Area | Existing entry points | Files to change or add |
|---|---|---|
| Tokens | `frontend/projects/common/src/styles/theme.css` | Same file: scoped Proton token block |
| Common tests | `frontend/projects/common/src/styles/theme.spec.ts` (if missing, add) | New file: token contract tests |
| Portal KB | `frontend/projects/portal-app/src/app/features/kb/kb-list.component.{ts,html,spec.ts}` | All three files |
| Portal home | `frontend/projects/portal-app/src/app/features/home/home.component.html` | Apply same hero style so landing leads into the search |
| Admin KB | `frontend/projects/admin-app/src/app/features/kb/kb-admin.component.{ts,html,spec.ts}` | All three files |
| Cross-cutting | `frontend/e2e/mockup-fidelity.spec.ts` | One test per feature |

## Per-task execution record

Each task file below is an executable checklist. Before implementation: add the
planned test name to the relevant spec file and observe the failure. After
implementation: update the task status, paste the focused test output and the
relevant build output, then mark the task complete. Never mark a task done from a
code read or an unrun build.
