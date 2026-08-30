# US-803 · Platform Settings Admin UI

| Field | Value |
|---|---|
| **Story** | `US-803` |
| **Epic** | [EPIC-09 Security & Administration](../epics/EPIC-09-administration.md) |
| **Feature** | [`FEAT-21` Security & Administration](../delivery-plan.md#feat-21--security-administration) |
| **Layer** | Frontend |
| **Ships with** | [US-303](./US-303-ticket-assignment.md) *(backend)* |
| **Actor** | Admin |
| **Priority** | P2 |
| **Sprint** | [12 — Administration](../delivery-plan.md#sprint-12-administration) · Slice S9 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-10.10 |
| **Spec criteria** | AC-803 |
| **Depends on** | [US-303](./US-303-ticket-assignment.md) |

## Story

**As an admin**, **I want** to manage platform settings through the UI, **so that** I can configure the system without direct database access.

## Business rules

- No BRD BR-n covers this directly. Platform settings are editable through a form-based UI.
- No BRD BR-n covers this directly. Settings changes are logged in the audit log.

## Acceptance criteria

#### AC1 — Settings page (spec AC-803)

Given an admin is logged in, when they navigate to platform settings, then a form with current settings values is displayed.

#### AC2 — Edit settings (spec AC-803)

Given the settings form, when the admin modifies a value and saves, then the setting is updated and a success confirmation is shown.

#### AC3 — Validation (spec AC-803)

Given the admin enters an invalid value (e.g. non-numeric for a numeric setting), when they attempt to save, then validation errors prevent the save.

#### AC4 — Audit logging (spec AC-803)

Given the admin saves a settings change, when the change is applied, then an audit log entry is created for the modification.

## SQL tables

None — frontend story. Consumes existing `PlatformSettingsController` endpoint and `PlatformSettings` table.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-803 | Component | `SettingsPageRendersForm` | Given the admin navigates to settings, when the page loads, then a form with current values is displayed. | Form with current values |
| TC-02 | AC-803 | Component | `SettingsSaveUpdatesValue` | Given the admin changes a setting, when they save, then a success toast appears and the new value persists. | Value updated, success toast |
| TC-03 | AC-803 | Component | `SettingsValidationBlocksInvalid` | Given the admin enters "abc" in a numeric field, when they save, then an error message prevents submission. | Validation error shown |
| TC-04 | AC-803 | Integration | `SettingsChangeLoggedToAudit` | Given the admin saves a change, when the audit log is queried, then an entry for the settings change exists. | Audit entry created |

## Notes

Ships with the existing `PlatformSettingsController` backend from the reference platform. This story adds the frontend UI only. Uses Angular reactive forms.

## Open questions

None.

## Status evidence

Shipped `FEAT-19`(admin) — `PlatformSettingsComponent`, list + inline per-row edit, consuming the
pre-existing `PlatformSettingsController` (no backend changes needed). See
`docs/superpowers/plans/EPIC-09-US-804-feat-21-administration/README.md`.

Status is set from what is committed and executed, never from what is planned.
