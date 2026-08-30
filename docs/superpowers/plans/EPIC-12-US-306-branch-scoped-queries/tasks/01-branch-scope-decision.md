# T1 — Resolve OQ-5 Before Branch-Scoped Queries

**Story:** `US-306`  
**Criteria:** AC-306.1, AC-306.2, AC-306.3; original AC-17  
**Status:** blocked on `OQ-5`  
**Commit:** pending  
**Test evidence:** none; not run by instruction

## Purpose

Close the product ambiguity before a filter can accidentally turn nullable grouping columns into a
security policy. This is a decision task, not an implementation task.

## Files and actions

1. Read `docs/brd/customer-support-crm-brd.md` (OQ-5),
   `docs/product/05-assumptions-and-open-questions.md`, and
   `docs/superpowers/specs/EPIC-13-US-311-organisation-structure.md`.
2. Record the decision in `docs/adr/NNNN-branch-visibility-scope.md` using
   `docs/adr/template.md`, or in an approved addendum to
   `docs/superpowers/specs/EPIC-13-EPIC-13-US-306-branch-scoped-queries.md`.
3. Decide branch ownership, null branch behaviour, multi-branch representation, administrator
   bypass/audit rules, null-row treatment, and whether current rows are populated sufficiently.
4. Update `docs/requirements/user-stories/EPIC-13-US-306-branch-scoped-queries.md` and this plan if the
   approved contract changes AC-17 or the `BranchId` data model.
5. Do not modify `TicketsController.cs`, `CustomersController.cs`, query handlers, repositories,
   migrations, or tests until the decision is approved.

## Gate output

The approved record must contain a concrete statement equivalent to:

```text
For authenticated user U, effective branch scope is derived from U's server-side identity.
The client cannot widen it. Admin bypass is [explicit rule], is [or is not] audited, and a
cross-branch detail read returns [exact status]. Rows with BranchId = null are [exact rule].
```

## Verification after approval

The next task must first add failing tests named `BranchUserSeesOnlyOwnBranchTickets` (AC-306.1),
`BranchUserSeesOnlyOwnBranchCustomers` (AC-306.1), `CrossBranchRowsAreExcludedOrRefused` (AC-306.2),
and `ApprovedAdminBypassIsExplicitAndAudited` (AC-306.3). Later commands, from `backend/`, are:

```powershell
dotnet test CustomerSupport.Tests/CustomerSupport.Tests.csproj --filter "FullyQualifiedName~BranchScopedQueries"
dotnet test CustomerSupport.slnx
dotnet build CustomerSupport.slnx --warnaserror
```

## Evidence / deviations

**Evidence:** blocked by unresolved OQ-5; no command output exists.  
**Deviations:** none. A decision that requires backfilling currently-null `BranchId` values must
create a separate approved migration/data task rather than hiding it in T2.
