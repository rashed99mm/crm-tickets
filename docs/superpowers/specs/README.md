# Canonical Specs

Specs are grouped conceptually by epic and story. New specs must use one of these names:

```text
EPIC-##-US-###-slug.md
EPIC-##-design-slug.md
```

The permanent story identifiers are owned by [`../../requirements/user-stories/`](../../requirements/user-stories/).
Acceptance criteria remain in the spec that owns them and are referenced by implementation plans.

## Current Canonical References

| Document | Scope |
|---|---|
| [`EPIC-12-US-000-as-built-alignment.md`](./EPIC-12-US-000-as-built-alignment.md) | Two .NET 10 hosts, authorization boundary, live chat, portal ticket detail, notifications, and verification |
| [`../../requirements/epics/`](../../requirements/epics/) | Epic-level business scope and story mapping |
| [`../../brd/customer-support-crm-brd.md`](../../brd/customer-support-crm-brd.md) | BRD requirements, rules, KPIs, risks, and open questions |

All existing specs have been migrated to the canonical names. Dates may remain in document history,
but new design work must not add a date to its filename.
