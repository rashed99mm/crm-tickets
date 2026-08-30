# US-314 · Per-Organisation Branding

| Field | Value |
|---|---|
| **Story** | `US-314` |
| **Epic** | [EPIC-04 Agent dashboard](../epics/EPIC-04.md) |
| **Feature** | [`FEAT-17` Localisation & branding](../delivery-plan.md#feat-17--localisation-branding) |
| **Layer** | Backend + Frontend |
| **Ships with** | — |
| **Actor** | Admin |
| **Priority** | P1 |
| **Sprint** | [14 — Localisation and branding](../delivery-plan.md#sprint-14-localisation-and-branding) · Slice S14 |
| **Estimate** | 5 points |
| **Status** | `not started` |
| **BRD requirements** | FR-12.9 |
| **Spec criteria** | AC-25 |
| **Depends on** | — |

## Story

**As an admin**, **I want** to configure logo and colors, **so that** the app reflects our brand.

## Business rules

- No BRD BR-n covers this directly. Per-organisation branding: logo, primary colour, and accent colour are configurable via admin settings and applied at runtime.

## Acceptance criteria

#### AC1 — Branding settings stored and returned by API (AC-25)

Given an admin calls the branding endpoint, when requesting current branding, then the API returns `logoUrl`, `primaryColor`, and `accentColor` values.

#### AC2 — Admin can update branding (AC-25)

Given an admin submits a valid branding payload, when the API processes the request, then the new branding values are persisted and returned.

#### AC3 — Frontend applies branding (AC-25)

Given branding settings exist, when the application loads, then CSS custom properties `--primary-color` and `--accent-color` are set and the logo is displayed in the sidebar/header.

## SQL tables

`PlatformSettings` — branding stored as settings entries:

```sql
CREATE TABLE [dbo].[PlatformSettings] (
    [Key]       NVARCHAR(200)  NOT NULL,
    [Value]     NVARCHAR(2000) NULL,
    [UpdatedAt] DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_PlatformSettings] PRIMARY KEY ([Key])
);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-25 | Integration | `GetBrandingReturnsCurrentSettings` | Given branding settings exist, when `GET /api/settings/branding` is called, then `logoUrl`, `primaryColor`, `accentColor` are returned | 200 with branding object |
| TC-02 | AC-25 | Integration | `UpdateBrandingPersistsValues` | Given a valid branding payload, when `PUT /api/settings/branding` is called, then the values are persisted | 200 with updated branding object |
| TC-03 | AC-25 | Integration | `UpdateBrandingRejectsInvalidColor` | Given an invalid color value (e.g. `not-a-color`), when `PUT /api/settings/branding` is called, then `400 Bad Request` is returned | 400 with validation error |
| TC-04 | AC-25 | Component | `BrandingAppliedToCssVariables` | Given branding settings are loaded, when the app renders, then CSS custom properties are set on the root element | `--primary-color` and `--accent-color` set |
| TC-05 | AC-25 | Component | `LogoDisplaysInSidebar` | Given a logo URL is configured, when the app renders, then the logo image is visible in the sidebar/header area | Logo image rendered |

## Notes

- Reuses the existing `PlatformSettings` infrastructure from the CCE Platform baseline.
- Color validation should accept hex (`#RRGGBB`) and optionally `rgb()`/`rgba()` formats.
- The frontend should fetch branding settings on app initialization and apply them before first paint to avoid a flash of default styles.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
