# Task 02 - Ticket SLA Automation Admin

**Status:** In progress  
**Closes gaps:** Escalation rules UI, auto-assignment rules UI, business hours UI, holidays UI, email/SMS alert config.

## Files

- Backend domain: `Entities/Sla/*`, new `Entities/Automation/*`
- Backend API: new `EscalationRulesController.cs`, `AssignmentRulesController.cs`, existing `BusinessHoursController.cs`
- Frontend API: `common/src/lib/organisation`, new `common/src/lib/automation`
- Frontend UI: `admin-app/src/app/features/organisation/*`, `features/admin/platform-settings.component.*`

## Implementation

- Add assignment and escalation rule CRUD.
- Add rule priority/reorder endpoint.
- Build business-hours calendar and holidays screen.
- Add SLA notification rules for email/SMS.
- Replace static routing rules table with real data.

## Code Example

```csharp
public sealed record AssignmentRuleDto(
    Guid Id,
    string Name,
    int Priority,
    string ConditionJson,
    Guid? DepartmentId,
    Guid? TeamId,
    Guid? AgentId,
    bool IsActive);
```

```html
@for (rule of assignmentRules(); track rule.id) {
  <tr>
    <td>{{ rule.priority }}</td>
    <td>{{ rule.name }}</td>
    <td><button type="button" (click)="edit(rule)">{{ 'action.edit' | t }}</button></td>
  </tr>
}
```

## Acceptance

- [ ] Admin can create/edit/delete/reorder assignment rules.
- [ ] Escalation rules are configurable by SLA policy and priority.
- [x] Business hours and holidays render from API and save changes.
- [ ] Email/SMS alerts can be enabled/disabled and audited.
- [ ] No static routing rows remain.

## Evidence

- Existing backend automation found in `SlaBreachDetector`, `SlaBreachScanner`, `BusinessHoursCalculator`, policy CRUD, business-hours calendar CRUD, public-holiday CRUD, pause/resume due-date shifting, breach event dedupe, and escalation-level progression.
- Added SLA admin screen sections for:
  - `GET /api/BusinessHours/calendars`
  - `POST /api/BusinessHours/calendars`
  - `GET /api/BusinessHours/holidays`
  - `POST /api/BusinessHours/holidays`
- Added shared frontend API types/methods for business-hour calendars and public holidays.
- Verified `npx ng test admin-app --watch=false --include=projects/admin-app/src/app/features/organisation/sla-policies.component.spec.ts` passed 2 tests, including create-and-refresh for business hours.
- Verified `npx ng build admin-app` passed with the existing dashboard unused-import warnings and initial bundle budget warning.
