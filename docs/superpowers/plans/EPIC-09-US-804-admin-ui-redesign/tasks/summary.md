# Admin UI redesign — execution record

**No tests written or run this pass** — explicit instruction ("skip testing for saving tokens").
This is a real deviation from this project's normal TDD convention, recorded rather than hidden.
`shell.component.spec.ts`'s two hardcoded references to the removed `/account/password` route were
updated to `/profile` so the existing test doesn't fail on an obviously-stale assertion, but no
suite was actually run to confirm.

## What shipped

1. **`CsDialog`** (`common/ui/dialog.component.ts`) — backdrop + panel modal, closes on backdrop
   click or Escape, focuses the panel on open. Exported from `public-api.ts`.
2. **Departments, SLA Policies, Users** — each screen's "create" form (and, for SLA Policies, the
   in-row edit form added 2026-08-27) moved from an always-visible inline `cs-card` into a
   `CsDialog` opened by an "Add"/"Edit" button. Dialogs close automatically on successful submit.
3. **Sidebar collapse** — `AdminShell` gained a `collapsed` signal (persisted in `localStorage` as
   `admin-shell:sidebar-collapsed`) and a toggle button pinned to the sidebar's edge. Collapsed
   state hides the app name, nav labels and identity name text, leaving icons only; a new
   `--spacing-sidebar-collapsed: 4.5rem` theme token backs the narrower width.
4. **Profile page** — new `ProfileComponent` at `/profile` (`features/account/profile.component.*`):
   identity section (name, role badges) plus the change-password form, moved in from the now-deleted
   `ChangePasswordComponent`. The sidebar's identity footer is now a link to `/profile` instead of
   static text. `/account/password` route removed; `nav.password` nav entry removed from
   `NAV_ITEMS` (a new `hidden` flag on `NavItem` lets `/profile` still resolve for the browser-tab
   title without appearing in the sidebar list, matching how the title-lookup table has always
   doubled as the nav source).

## Known gap

Nothing has been visually verified in a browser this pass, and no unit/component tests were run —
per the explicit instruction. Before this is trusted, at minimum: `npx ng build admin-app` and a
manual click-through of the four changed screens.
