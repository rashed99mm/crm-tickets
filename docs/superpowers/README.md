# Superpowers Documentation

This is the canonical implementation documentation for the Customer Support CRM.

## Naming

Documents are organized by epic and story, not by work date:

```text
EPIC-03-US-201-record-message.md
EPIC-03-US-201-record-message/
  implementation-plan.md
  tasks/
```

`EPIC-##` is the product area. `US-###` is the permanent requirement identifier from
[`../requirements/user-stories/`](../requirements/user-stories/). A design document that supports
several stories belongs in the epic directory and uses `EPIC-##-design-<slug>.md`.

## Source Of Truth

1. [`../brd/customer-support-crm-brd.md`](../brd/customer-support-crm-brd.md) owns objectives,
   functional requirements, business rules, KPIs, risks, and open questions.
2. [`../requirements/`](../requirements/) owns epic and story decomposition and delivery status.
3. [`specs/`](./specs/) owns acceptance criteria and contract-level behavior.
4. [`plans/`](./plans/) owns implementation sequencing, code touchpoints, and verification.
5. [`plans/EPIC-12-US-000-as-built-alignment.md`](./plans/EPIC-12-US-000-as-built-alignment.md)
   records the implemented cross-cutting behavior from the final integration pass.

All plan and spec paths have been migrated to the canonical names above. Dates may remain inside
document history and delivery notes, but must not be used in new filenames.

## Epic Map

| Epic | Area | Requirements |
|---|---|---|
| `EPIC-01` | Foundation and platform | `US-101`-`US-111`, `US-108`-`US-110` |
| `EPIC-02` | Ticket management | `US-009`-`US-038`, `US-118`-`US-143` |
| `EPIC-03` | Communication channels | `US-201`-`US-205`, `US-202` |
| `EPIC-04` | Agent workspace and dashboard | `US-035`, `US-606`, `US-607` |
| `EPIC-05` | SLA and automation | `US-210`-`US-225` |
| `EPIC-06` | Knowledge base | `US-501`-`US-513` |
| `EPIC-07` | Customer portal | `US-401`-`US-415` |
| `EPIC-08` | Reporting | `US-601`-`US-610` |
| `EPIC-09` | Administration and security | `US-801`-`US-805`, `US-112`-`US-117` |
| `EPIC-10` | Integrations | `US-203`-`US-205`, `US-144` |
| `EPIC-11` | AI assistance | `US-701`-`US-708` |
| `EPIC-12` | Cross-cutting platform alignment | `US-101`-`US-111`, `US-122`, `US-129` |
| `EPIC-13` | Mockup and responsive fidelity | `US-311`-`US-314` |
