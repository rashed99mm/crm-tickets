# EPIC-12-US-306 Branch-Scoped Queries: As-Built Execution Plan

**Status:** Implemented in the application layer; focused integration verification is ready.

## Goal

Give internal users with a persisted `ApplicationUser.BranchId` access only to tickets and
customers in that branch, while keeping users without a branch assignment unscoped. Keep
department assignment available for grouping and routing without making departments an
authorization boundary.

## Product decisions

- `ApplicationUser.BranchId` is the source of truth for staff visibility.
- A null branch means unscoped access, supporting administrators and existing data migration.
- Department and team values are assignment metadata. Department-based visibility is not enabled.
- Out-of-branch detail reads return the normal not-found result rather than revealing existence.
- Portal submit, tracking, history, FAQ, and feedback remain customer-owned flows.

## Runtime flow

1. Authentication identifies the caller.
2. `IIdentityUserService` loads the persisted `ApplicationUser`.
3. Create handlers inherit the caller's branch onto new customers and tickets.
4. Ticket assignment inherits the target agent's department, branch, and team.
5. List handlers add a branch predicate only when the caller has a branch.
6. Detail handlers return not-found for a branch mismatch.

## Real code examples

Customer creation records the caller's branch in the handler, not the controller:

```csharp
var customer = Customer.Create(request.Name, request.Email, request.Phone);
var actor = await identityUsers.GetByIdAsync(userContext.UserId, cancellationToken);

if (actor?.BranchId is Guid branchId)
    customer.AssignBranch(branchId);

await customers.AddAsync(customer, cancellationToken);
```

The list query composes scope with existing filters:

```csharp
var branchId = actor?.BranchId;
var filter = BuildFilter(request);

if (branchId.HasValue)
    filter = filter.And(customer => customer.BranchId == branchId.Value);
```

The ticket list applies the same policy through the repository query:

```csharp
query = query.WhereIf(branchId.HasValue,
    ticket => ticket.BranchId == branchId!.Value);
```

The domain owns the mutation:

```csharp
public void AssignBranch(Guid? branchId)
{
    BranchId = branchId;
    MarkUpdated();
}
```

## Execution checklist

- [x] Add nullable organisation fields and migration support.
- [x] Inherit the acting user's branch during customer and ticket creation.
- [x] Inherit the target agent's organisation during ticket assignment.
- [x] Scope ticket and customer list/detail queries.
- [x] Keep controllers thin; policy lives in handlers and domain methods.
- [x] Add integration coverage for branch-owned and unscoped records.
- [ ] Run the focused test while API binaries are not locked by running hosts.

## Acceptance mapping

| Criterion | Implementation | Verification |
|---|---|---|
| AC-17 branch ticket/customer lists | Ticket/customer list handlers | `AC17_BranchUser_SeesOnlyTicketsAndCustomersInOwnBranch` |
| AC-17 branch detail isolation | Ticket/customer detail handlers | Same test asserts 404 for out-of-branch details |
| Unscoped admin visibility | Predicate is added only when `BranchId` has a value | Existing admin behavior and integration setup |
| Organisation inheritance | Customer/ticket create and ticket assignment handlers | Integration setup assigns a branch to the agent |

## Verification command

```powershell
dotnet test backend/tests/CustomerSupport.Tests/CustomerSupport.Tests.csproj --filter "FullyQualifiedName~OrganisationStructureEndpointTests"
```

If development APIs are running from the normal build output, stop them first or use an isolated
output directory. MSBuild cannot replace locked application assemblies.

