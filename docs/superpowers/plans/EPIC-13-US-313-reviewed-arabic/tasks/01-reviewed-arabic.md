# T1 — Arabic Catalogue Audit and Review Preparation

**Story:** `US-313`  
**Criteria:** AC-313.1, AC-313.2; original AC-24  
**Status:** not started  
**Commit:** pending  
**Test evidence:** none; not run by instruction

## Files

- `frontend/projects/common/src/lib/i18n/translations.ts`;
- `frontend/projects/common/src/lib/i18n/bilingual-ui.spec.ts`;
- `frontend/projects/common/src/lib/i18n/locale.store.spec.ts`;
- `frontend/projects/common/src/lib/testing/no-hardcoded-strings.spec.ts`;
- both app route/component inventories and `frontend/e2e/journey.spec.ts`.

## Work

1. Enumerate every `TranslationKey` and every template pipe/key use in `common`, `admin-app`, and
   `portal-app`, including loading, empty, error, validation, and accessibility text.
2. Add failing tests named `AC313_1_ArabicCatalogueContainsReviewedValues`,
   `AC313_2_EnglishAndArabicKeysMatch`, `AC313_2_NoRawTranslationKeysInRenderedHtml`, and
   `AC313_1_LocaleSwitchDoesNotRefetch`.
3. Mark placeholder, transliterated, awkward, and empty Arabic values for native-speaker review;
   do not mask them with English fallback.
4. Replace values in `translations.ts` only after review, preserving `{0}` placeholders and key names.
5. Record reviewer sign-off for all screens and verify server bilingual messages still use
   `LocaleStore.resolve()`.

## Later verification

```powershell
cd frontend
npx ng test common --watch=false
npx ng test admin-app --watch=false
npx ng test portal-app --watch=false
npx ng build admin-app
npx ng build portal-app
npx playwright test --grep "Arabic|language|locale"
```

## Evidence / deviations

**Evidence:** pending catalogue diff, test output, and named native-speaker sign-off.  
**Deviations:** none.
