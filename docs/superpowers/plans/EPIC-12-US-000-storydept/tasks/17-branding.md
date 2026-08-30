# Task 17 — Custom branding (US-314)

## Traceability
Epic:   docs/requirements/epics/EPIC-12-platform.md
Story:  docs/requirements/user-stories/EPIC-13-US-314-branding.md
FEAT:   FEAT-23 — delivery-plan.md row 14
Plan:   docs/superpowers/plans/EPIC-13-US-314-branding/

## Work
PlatformSettings keys: brand.logoUrl, brand.productName, brand.accent (platform-settings module
+ PlatformSettingApi already exist). Admin settings UI gets a Branding section; both shells
(admin + portal public shell) read logo/product name/accent from settings — accent applied as a
CSS custom property override, brand name replaces the hardcoded landing/brand strings
('landing.brand' / 'portal.name').

## Tests
AC314_BrandOverridesRenderFromSettings — settings change reflected in both shells.

## Gate
common + admin + portal suites green.
