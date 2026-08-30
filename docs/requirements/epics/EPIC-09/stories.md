# EPIC-09 · Security & Administration — Stories (All Slices)

| Epic | Slice(s) | BRD Requirements |
|---|---|---|
| `EPIC-09` | S1, S9 | FR-10.1–FR-10.11 |

---

## S1 — Authentication & Authorization (Done)

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-112 | Staff sign-in with role-carrying JWT | Backend | S1 | P0 | `done` | FR-10.1, AC-1, AC-3 |
| US-113 | Failed sign-in reveals nothing (lockout indistinguishable) | Backend | S1 | P0 | `done` | FR-10.2, FR-10.3, AC-2, AC-6, AC-67 |
| US-114 | Role permissions refuse (403 for wrong role) | Backend | S1 | P0 | `done` | FR-10.5, AC-4 |
| US-115 | Credentials never emitted in any response/log | Backend | S1 | P0 | `done` | FR-10.4, AC-5, NFR-5 |
| US-125 | Sign-in screen (admin-app) | Frontend | S1 | P0 | `done` | AC-55, AC-56 |
| — | Staff management: create, list, activate/deactivate | Frontend | S1 | P0 | `done` | FR-10.7 |
| — | Change own password | Frontend | S1 | P1 | `done` | FR-10.1 |

---

## S9 — User Management (Full)

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| — | Deactivate user without deleting (preserve attribution) | Backend | S9 | M | `done` | FR-10.11 |
| — | User management admin UI (full CRUD) | Frontend | S9 | M | `partial` | FR-10.7 |
| — | Bulk user operations (import/export) | Backend | S9 | S | `not started` | FR-10.7 |

---

## S9 — Granular Permissions

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-804 | Permission entity (Name, Description, Category) | Backend | S9 | S | `partial` | FR-10.8 |
| — | Role-Permission mapping (many-to-many) | Backend | S9 | S | `done` | FR-10.8 |
| — | Dynamic policy builder from permissions | Backend | S9 | S | `not started` | FR-10.8 |
| US-805 | Permission management admin UI | Frontend | S9 | S | `done` | FR-10.8 |
| — | Role editor with permission assignment | Frontend | S9 | S | `not started` | FR-10.8 |

## S9 — OTP Verification

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| — | OTP verification through Email and SMS integration URLs | Backend | S9 | P1 | `not started` | FR-10.1, FR-3.2 |

Canonical documents:
[`EPIC-09-US-112-otp-verification-design.md`](../../superpowers/specs/EPIC-09-US-112-otp-verification-design.md)
and [`EPIC-09-US-112-otp-verification/`](../../superpowers/plans/EPIC-09-US-112-otp-verification/).
OTP depends on the Sprint 9 notification gateway and must not create a second provider abstraction.

---

## S9 — System Audit Log

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| — | Audit log entries already written (AuditBehavior) | Backend | S9 | M | `done` | FR-10.9 |
| US-801 | Audit log query endpoint (filter by user, action, entity, date) | Backend | S9 | M | `done` | FR-10.9 |
| US-802 | Audit log viewer admin UI | Frontend | S9 | M | `done` | FR-10.9 |
| — | Export audit log | Backend | S9 | S | `not started` | FR-10.9 |

---

## S9 — System Configuration

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| — | Platform settings CRUD (API exists) | Backend | S9 | S | `done` | FR-10.10 |
| US-803 | Platform settings admin UI | Frontend | S9 | S | `done` | FR-10.10 |
| — | Configuration change audit trail | Backend | S9 | S | `not started` | FR-10.10 |

---

## Summary

| Category | Total Stories | Done | Not Started |
|---|---|---|---|
| S1 Auth | 7 | 7 | 0 |
| S9 User Management | 3 | 1 | 1 |
| S9 Permissions | 5 | 2 | 2 |
| S9 Audit Log | 4 | 3 | 1 |
| S9 Configuration | 3 | 2 | 1 |
| **Total** | **22** | **15** | **5** |

`partial` stories counted in neither column: the user-management admin UI (S9) and US-804
(permission entity). Statuses corrected 2026-08-29 to match the delivery plan: US-801/802/803
shipped earlier, the permission matrix endpoints + admin UI (US-805) are exercised, and the
admin users screen was enriched (paging, status/role filters, search, sorts, CSV export; backend
`role` filter on `GET /api/Users`).
