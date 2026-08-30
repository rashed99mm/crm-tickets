# Task 16 — Responsive layout + RTL + reviewed Arabic (US-311/312/313)

## Traceability
Epic:   docs/requirements/epics/EPIC-12-platform.md
Stories: EPIC-13-US-311-responsive-layout.md, EPIC-13-US-312-rtl-layout.md, EPIC-13-US-313-reviewed-arabic.md
FEAT:   FEAT-23 — delivery-plan.md row 14
Specs:  docs/superpowers/specs/EPIC-13-EPIC-13-US-311-responsive-layout.md,
        docs/superpowers/specs/EPIC-13-EPIC-13-US-312-rtl-layout.md,
        docs/superpowers/specs/EPIC-13-EPIC-13-US-313-reviewed-arabic.md

## Spec findings

**US-311 Responsive (AC-22):** Primary screens usable at 375/768/1280px, no horizontal overflow,
sidebar collapses accessibly, tables/forms preserve content. Tailwind breakpoints + logical
properties.

**US-312 RTL (AC-23):** Full layout mirror: sidebar on right, text-align right, logical CSS
properties throughout. RTL guard tests exist in test suite.

**US-313 Arabic (AC-24):** All visible strings are reviewed natural Arabic, no raw key pattern
(ALL_CAPS_UNDERSCORED) in DOM. TC-03 (manual native-speaker review) cannot be automated.

## Audit findings (2026-08-28)

1. `translations.ts` — grep for `????` returns **zero matches**. All Arabic strings are filled
   with real Arabic text (e.g. `ar: 'مكتب الدعم'`, `ar: 'لوحة العمل'`, etc.). The plan's
   assumption of placeholder copy is **incorrect for the current state of the file**. No `????`
   developer placeholders remain.

2. `dir="rtl" lang="ar"` — check `app.component.ts` for `<html>` attribute binding.

3. Logical properties — grep for physical `margin-left|margin-right|padding-left|padding-right`
   in component templates.

4. Responsive breakpoints — verify at 375/768/1280px manually (cannot be automated in CI).

## Work (if any gaps found)

1. If physical CSS properties found → replace with logical equivalents (ms-/me-/ps-/pe-).
2. If `dir=rtl` not set on `<html>` → add locale-to-dir binding in app bootstrap.
3. RTL guard tests stay green (existing test suite validates AC-23).
4. US-313 TC-03 is manual — record as not verified (requires human reviewer).

## Gate
- [x] `translations.ts` has no `????` placeholders (grep confirmed 2026-08-28).
- [ ] RTL guard tests green: `npx ng test admin-app --watch=false --include "**/rtl*.spec.ts"` (run if exists).
- [ ] Manual viewport check: 375px / 768px / 1280px — no horizontal overflow on queue, detail, dashboard.
- [ ] `dir=rtl` present on `<html>` when Arabic locale active.
