# Task 01 - Live Chat, AI, Branding, KB, Admin Exports

**Status:** Implemented with known unrelated test-suite failures  
**Spec:** `docs/superpowers/specs/EPIC-13-US-311-ui-gap-closure-sdd.md`  
**Acceptance:** AC-GAP-01 through AC-GAP-12

## Work Items

- [x] Replace the static chat-session transcript with `ChatStore` loading/error/empty/loaded states.
- [x] Wire chat composer submit to `ChatStore.sendMessage`.
- [x] Wire chat session close to `ChatApi.closeSession`.
- [x] Replace static chat AI sidebar content with transcript-derived summary and insertable drafts.
- [x] Fix AI suggested article links to an existing admin KB route.
- [x] Make the AI draft reply action visible with the explicit `ai.draftReply` label.
- [x] Fix platform branding controls so `logoUrl` and `accentColor` bind to matching fields.
- [x] Add a real image file input for logo selection and preview.
- [x] Apply saved branding immediately through `BrandingStore`.
- [x] Render editable generic platform settings rows.
- [x] Add KB category picker and persist the selected category.
- [x] Render KB version history in edit mode.
- [x] Calculate KB insights from loaded article data.
- [x] Add CSV export for audit log and user list current views.
- [x] Replace inert user action menu with activate/deactivate.
- [x] Render department name when the users API returns it.
- [x] Add forgot-password support links to admin and portal login screens.

## Evidence

- `npx ng build admin-app`: passed. Remaining warnings are existing dashboard unused imports and bundle budget overage.
- `npx ng build portal-app`: passed. Remaining warning is existing bundle budget overage.
- `npx ng test admin-app --watch=false`: compiled and ran 189 tests; 177 passed, 12 failed. Failures are in pre-existing dashboard/error-state specs (`dashboard.component.spec.ts`, `ticket-messages.component.spec.ts`, `permissions.component.spec.ts`, `customer-detail.component.spec.ts`). The KB failures introduced by category persistence were fixed and did not recur on rerun.
- First verification attempt inside the sandbox failed with `spawn EPERM`; the commands were rerun with escalation so Angular/esbuild could spawn its worker.
