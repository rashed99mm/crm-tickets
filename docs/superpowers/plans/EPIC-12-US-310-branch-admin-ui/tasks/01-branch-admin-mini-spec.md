# T1 — Branch Administration Mini-Spec Gate

**Story:** `US-310`  
**Criteria:** AC-310.1, AC-310.2; original AC-21  
**Status:** blocked until explicit approval  
**Commit:** pending  
**Test evidence:** none; not run by instruction

## Work

Create an approved mini-spec before implementation. It must name:

- actor and server permission: Admin only for create/update/deactivate;
- route: `/branches`, shell placement, navigation label and guard;
- list response and columns: `id`, `name`, `region`, `timezone`, `isActive`, `createdAt`;
- request contract: `POST`/`PUT /api/Branches` payload with `name`, `region`, `timezone`;
- validation: whitespace, 200-character name limit, timezone rule, and keyed envelope errors;
- lifecycle: active/inactive display, confirmation, repeated deactivation, and whether inactive
  rows are returned;
- loading, empty, error/retry, 401/403/404 states and focus management;
- responsive breakpoints, table strategy, Arabic/RTL labels, and accessibility semantics.

## Existing contract to verify, not silently change

`backend/src/CustomerSupport.InternalApi/Controllers/BranchesController.cs` currently documents
`GET /api/Branches`, `GET /api/Branches/{id}`, `POST /api/Branches`, `PUT /api/Branches/{id}`, and
`DELETE /api/Branches/{id}`. `BranchRequest` is in
`backend/src/CustomerSupport.Application/Features/Organisation/Dtos/BranchDtos.cs`; the API uses
the standard `Response<T>` envelope and `PaginatedList<T>`. The mini-spec must record any mismatch
instead of making the UI compensate for it.

## Gate output

Write the approved artifact at `docs/superpowers/specs/EPIC-13-US-310-branch-admin-ui-addendum.md` or a
numbered ADR, then update the implementation plan and create any newly required task records.
Approval is explicit and recorded; “the story is obvious” is not approval.

## Later verification

After T2/T3, from `frontend/`:

```powershell
npx ng test common --watch=false
npx ng test admin-app --watch=false
npx ng build admin-app
npx playwright test --grep "branch"
```

## Evidence / deviations

**Evidence:** blocked; no mini-spec approval or command output.  
**Deviations:** none. Do not mark this task done merely because the existing controller is usable.
