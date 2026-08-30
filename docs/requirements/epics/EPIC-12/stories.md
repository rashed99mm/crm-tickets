# EPIC-12 · Platform Features — Stories (All Slices)

| Epic | Slice(s) | BRD Requirements |
|---|---|---|
| `EPIC-12` | S1, S8 | FR-12.1–FR-12.10 |

---

## S1 — Foundation (Done)

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-101 | Uniform response envelope | Backend | S1 | P0 | `done` | FND-1–FND-5 |
| US-102 | Outcome-to-status mapping (one place) | Backend | S1 | P0 | `done` | FND-4, FND-8 |
| US-103 | TraceId + timestamp on every response | Backend | S1 | P1 | `done` | FND-6, FND-7 |
| US-104 | Field-keyed validation errors | Backend | S1 | P0 | `done` | FND-9–FND-11 |
| US-105 | Reflection-free validation pipeline | Backend | S1 | P0 | `done` | FND-12, FND-13 |
| US-106 | Bilingual message catalogue (YAML) | Backend | S1 | P0 | `done` | FND-14–FND-17, FND-20 |
| US-107 | Build fails when code has no message | Backend | S1 | P0 | `done` | FND-18, FND-19, FND-21 |
| US-108 | Domain base types (identity, equality) | Backend | S1 | P1 | `done` | FND-22, FND-27, FND-28 |
| US-109 | Auditing + soft delete (automatic) | Backend | S1 | P0 | `done` | FND-23–FND-26 |
| US-110 | Dependency rule enforced by build | Backend | S1 | P0 | `done` | FND-29 |
| US-111 | API self-documentation + health endpoint | Backend | S1 | P1 | `done` | FND-30–FND-32 |
| US-122 | Stable code per condition (AC-51) | Backend | S1 | P0 | `done` | AC-51 |
| US-123 | Diagnosable without leaking (AC-52, AC-53) | Backend | S1 | P1 | `done` | AC-52, AC-53 |
| US-124 | Unambiguous wire format (AC-54) | Backend | S1 | P1 | `done` | AC-54 |
| US-093 | Bilingual instant switching | Frontend | S1 | P1 | `done` | AC-63, AC-68 |

---

## S8 — Localisation & Layout

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-313 | Reviewed Arabic translation (replace placeholders) | Frontend | S8 | M | `not started` | FR-12.5, DEP-5 |
| US-312 | Full RTL layout correctness (physical direction CSS audit) | Frontend | S8 | M | `not started` | FR-12.4 |
| US-311 | Responsive layout: sidebar collapse on mobile | Frontend | S8 | M | `not started` | FR-12.6 |
| US-311 | Responsive layout: 360px–1440px breakpoint coverage | Frontend | S8 | M | `not started` | FR-12.6, NFR-15 |
| US-314 | Per-organisation branding (logo + colors) | Backend | S8 | S | `not started` | FR-12.9 |
| US-314 | Branding configuration UI | Frontend | S8 | S | `not started` | FR-12.9 |

---

## S8 — Department & Branch

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-301 | `Department` entity + EF migration | Backend | S8 | M | `not started` | FR-12.7 |
| US-302 | `Branch` entity + EF migration | Backend | S8 | M | `not started` | FR-12.8 |
| US-303 | Add `DepartmentId` to User, Ticket, Category | Backend | S8 | M | `not started` | FR-12.7 |
| US-304 | Add `BranchId` to User, Ticket, Customer | Backend | S8 | M | `not started` | FR-12.8 |
| US-305 | Seed default department + branch | Backend | S8 | M | `not started` | FR-12.7, FR-12.8 |
| US-306 | Branch-scoped query filters (Tickets, Customers) | Backend | S8 | M | `not started` | FR-12.8 |
| US-307 | `DepartmentsController` (CRUD) | Backend | S8 | M | `not started` | FR-12.7 |
| US-308 | `BranchesController` (CRUD) | Backend | S8 | M | `not started` | FR-12.8 |
| US-309 | Department management admin UI | Frontend | S8 | M | `not started` | FR-12.7 |
| US-310 | Branch management admin UI | Frontend | S8 | M | `not started` | FR-12.8 |
| — | User assignment to department + branch | Backend | S8 | M | `not started` | FR-12.7, FR-12.8 |
| — | Department/branch selectors on user create/edit | Frontend | S8 | M | `not started` | FR-12.7, FR-12.8 |

---

## Summary

| Category | Total Stories | Done | Not Started |
|---|---|---|---|
| S1 Foundation | 15 | 15 | 0 |
| S8 Localisation | 6 | 0 | 6 |
| S8 Department/Branch | 12 | 0 | 12 |
| **Total** | **33** | **15** | **18** |
