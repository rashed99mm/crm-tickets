# Task 05 · Adapt customer and administration surfaces

**Criteria:** `AC-410`, `AC-411`, `AC-416`, `AC-417`, `AC-418`  
**Status:** Completed (All customer profile, users and KB specs passed)

## Changes

Adapt customer profile/360 history, admin dashboard/table, admin ticket management and knowledge
base management. Preserve customer, user, content and permission API contracts. Keep the designed
rails and table density even where fields are not backed by current DTOs.

## Test-first cases

- `AC410_CustomerProfileUsesIdentityBandAndThreeRegionWorkspace`
- `AC411_AdminAndKnowledgeBaseScreensPreserveReferenceHierarchy`
- `AC416_CustomerAndAdminScreensShowDistinctAsyncStates`
- `AC417_UnbackedCustomerFieldsAreReadOnlyUnavailableStates`
- `AC418_AdminTablesAndRailsAreKeyboardAccessible`

## Done when

Customer and administration screens match their governing references, RTL checks pass, and no
fabricated data or unsupported mutation control has been introduced.

## Exact files

- Customer workspace: `frontend/projects/admin-app/src/app/features/customers/customer-detail.component.{ts,html,spec.ts}`.
- Customer rails: `customer-notes.component.{ts,html,spec.ts}` and
  `customer-attachments.component.{ts,html,spec.ts}`.
- Customer table: `customer-list.component.{ts,html,spec.ts}`.
- Admin tables: `frontend/projects/admin-app/src/app/features/users/users.component.{ts,html,spec.ts}`
  and `features/admin/{audit-log,permissions,platform-settings}.component.*`.
- Knowledge base: `frontend/projects/portal-app/src/app/features/kb/{kb-list,kb-detail}.component.*`.
- References: `stitch_smart_support_ticketing_crm/{customer_profile_history,customer_360_history,admin_ticket_management,knowledge_base_management}/code.html`.

## Live implementation example

In `customer-detail.component.html`, preserve the existing customer signal and child component
inputs, but change the outer layout to the reference identity band plus `grid-cols-[3fr_6fr_3fr]`
desktop workspace. At mobile widths switch to one column in the order identity, contact, activity,
files. Render DTO-missing fields with `CsPlaceholder`; do not hardcode a company or job title.

## Execution commands

```text
cd frontend
npx ng test admin-app --watch=false --include='**/features/customers/**/*.spec.ts'
npx ng test admin-app --watch=false --include='**/features/users/**/*.spec.ts'
npx ng test portal-app --watch=false --include='**/features/kb/**/*.spec.ts'
```
