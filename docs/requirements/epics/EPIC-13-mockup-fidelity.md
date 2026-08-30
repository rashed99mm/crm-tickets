# EPIC-13 · Mockup-faithful responsive UI

| | |
|---|---|
| **Epic** | `EPIC-13` |
| **Priority** | P0 (design delivery) |
| **Stories** | Cross-cutting screen adaptation; no new backend stories |
| **Screens** | 16 supplied screen references across `admin-app`, `portal-app` and `common` |
| **Criteria** | `AC-400`…`AC-422` — see [`../../superpowers/specs/EPIC-11-US-701-mockup-faithful-ui.md`](../../superpowers/specs/EPIC-11-US-701-mockup-faithful-ui.md) |
| **Status** | `not started` |

## Goal

Adapt every supplied Stitch screen into the existing Angular applications with the same composition,
typography, spacing, palette, visual states and responsive behaviour. This is a frontend-only,
cross-cutting epic over existing features; it does not claim new backend capabilities.

## Styling decision

Tailwind v4 is retained. It is already configured in `frontend/.postcssrc.json` and is also the
format used by the supplied mockup HTML. The implementation will port the markup into standalone
Angular templates and centralise the mockup tokens in `common/src/styles/theme.css`. No CDN runtime
stylesheet and no second CSS framework will be introduced.

The mockups contain two palettes. Command Center and Proton Precision remain scoped separately, as
approved in the spec. Semantic status and priority colours remain meaningful in both palettes.

## Delivery slices

| Slice | Scope | Criteria |
|---|---|---|
| 0 | Token scopes, shared shell, drawer, header and state primitives | `AC-400`…`AC-404`, `AC-418` |
| 1 | Command Center shell, landing and authentication | `AC-405`, `AC-412` |
| 2 | Ticket queue, create, detail and AI workspaces | `AC-407`…`AC-409` |
| 3 | Customer and administration surfaces | `AC-410`, `AC-411` |
| 4 | Dashboards, analytics and portal home/profile | `AC-406`, `AC-411`, `AC-412` |
| 5 | Responsive matrix, visual review and regression closure | `AC-413`…`AC-422` |

## Dependencies and boundary

The existing feature APIs and route contracts are dependencies. Missing data is represented using the
spec's non-interactive unavailable state rather than fabricated data or an unplanned endpoint. Each
slice must preserve existing signals, HTTP behaviour, i18n and accessibility tests.

## Definition of done

Every slice has a reviewed implementation plan, failing tests written before implementation, passing
component tests, clean application builds, reviewed screenshots at all four target widths, and a
task record containing actual test evidence and deviations.
