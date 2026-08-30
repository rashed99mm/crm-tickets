# US-313 Reviewed Arabic — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace placeholder Arabic (`PA-7`) with reviewed product copy across the shared dictionary and
both apps, preserving typed key parity, bilingual server messages, and the no-refetch locale switch.

**Architecture:** One file — `frontend/projects/common/src/lib/i18n/translations.ts` — owns all
client strings as `{ en, ar }`. `TranslationKey` is `keyof typeof TRANSLATIONS`, so keys are
compile-time checked everywhere (`TranslatePipe`, `LocaleStore.t`, shells, shared UI). Server messages
stay bilingual in the envelope and go through `LocaleStore.resolve()` / the `localize` pipe — they are
NOT duplicated here.

**Tech Stack:** Angular 20, `LocaleStore`, typed `TranslationKey`.

**Spec:** `docs/superpowers/specs/EPIC-13-EPIC-13-US-313-reviewed-arabic.md`

**Not implemented this pass.** This plan is written ahead of any code that implements it, per explicit
instruction — execution is a future session's work. The Arabic below is *illustrative reviewed copy*;
the real deployment requires native-speaker sign-off recorded in the story evidence before the file is
edited.

---

## Global Constraints

- Keep exactly one `LocalizedMessage` per key. No second language file, no hardcoded template strings
  (the `no-hardcoded-strings.spec.ts` guard enforces this).
- Every key must have non-empty, natural English **and** Arabic. The placeholder scan in Task 1 flags
  any empty Arabic or any value that equals its English (a tell-tale of "didn't translate").
- `{0}`, `{1}` … placeholders must survive the copy edit positionally — `LocaleStore.t`
  leaves an unsupplied slot visible, so a moved argument must still line up.
- `LocaleStore.setLocale()` remains the only switch. Both languages already arrive in API envelopes;
  changing locale updates rendered text + `dir`/`lang` with no HTTP request. The "no refetch" test
  below asserts exactly that.

---

### Task 1: Inventory + failing audits (`AC-313.1`, `AC-313.2`)

**Files:**
- Create: `frontend/projects/common/src/lib/i18n/translations.audit.spec.ts`
- Modify: `frontend/projects/common/src/lib/i18n/bilingual-ui.spec.ts` (extend if needed)

**Interfaces:**
- Consumes: `TRANSLATIONS` and `TranslationKey` from `./translations`.

- [ ] **Step 1: Write the failing audits**

```ts
// frontend/projects/common/src/lib/i18n/translations.audit.spec.ts
import { TRANSLATIONS } from './translations';

describe('Reviewed Arabic catalogue (AC-313)', () => {
  const keys = Object.keys(TRANSLATIONS) as (keyof typeof TRANSLATIONS)[];

  it('AC313_1: every key has non-empty English and Arabic', () => {
    const empty = keys.filter(
      (k) => !TRANSLATIONS[k].en?.trim() || !TRANSLATIONS[k].ar?.trim(),
    );
    expect(empty).toEqual([]);
  });

  it('AC313_1: Arabic is reviewed, not a copy of English (placeholder scan)', () => {
    const copied = keys.filter((k) => TRANSLATIONS[k].ar.trim() === TRANSLATIONS[k].en.trim());
    expect(copied).toEqual([]);
  });

  it('AC313_2: English and Arabic key sets match (parity)', () => {
    // TranslationKey is the single source, so parity is structural — assert both halves exist.
    for (const k of keys) {
      expect(TRANSLATIONS[k]).toEqual(
        jasmine.objectContaining({ en: jasmine.any(String), ar: jasmine.any(String) }),
      );
    }
  });

  it('AC313_2: keeps {n} placeholders in both halves', () => {
    const mismatch = keys.filter((k) => {
      const en = (TRANSLATIONS[k].en.match(/\{(\d+)\}/g) ?? []).sort().join(',');
      const ar = (TRANSLATIONS[k].ar.match(/\{(\d+)\}/g) ?? []).sort().join(',');
      return en !== ar;
    });
    expect(mismatch).toEqual([]);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx ng test common --watch=false --include='**/translations.audit.spec.ts'`
Expected: FAIL — the catalogue currently ships `PA-7` developer placeholder Arabic in many keys, so the
"not a copy of English" and possibly "non-empty" checks fail.

- [ ] **Step 3: Commit the red audit**

```bash
git add frontend/projects/common/src/lib/i18n/translations.audit.spec.ts
git commit -m "test(arabic): failing reviewed-catalogue audits (US-313 T1)"
```

---

### Task 2: Replace Arabic values with reviewed copy (`AC-313.1`, `AC-313.2`)

**Files:**
- Modify: `frontend/projects/common/src/lib/i18n/translations.ts`

**Interfaces:**
- Produces: the same `TranslationKey` set, now with reviewed `ar` values.

- [ ] **Step 1: Apply reviewed Arabic to the catalogue**

The placeholder header comment (`PA-7`) is deleted and each `ar:` value is replaced with the
native-speaker-reviewed string. Representative reviewed entries (the full file receives the same
treatment for every key, including auth, tickets, customers, reports, settings, permissions, AI,
attachments, validation, pagination, loading/empty/error states):

```ts
export const TRANSLATIONS = {
  // ---- Product and chrome ----
  'app.name': { en: 'Support Desk', ar: 'مكتب الدعم' },
  'portal.name': { en: 'Support', ar: 'الدعم' },

  // ---- Navigation (admin) ----
  'nav.dashboard': { en: 'Dashboard', ar: 'لوحة التحكم' },
  'nav.tickets': { en: 'Tickets', ar: 'التذاكر' },
  'nav.customers': { en: 'Customers', ar: 'العملاء' },
  'nav.staff': { en: 'Staff', ar: 'الموظفون' },
  'nav.departments': { en: 'Departments', ar: 'الأقسام' },
  'nav.slaPolicies': { en: 'SLA Policies', ar: 'سياسات مستوى الخدمة' },
  'nav.reports': { en: 'Reports', ar: 'التقارير' },
  'nav.auditLog': { en: 'Audit Log', ar: 'سجل التدقيق' },
  'nav.settings': { en: 'Settings', ar: 'الإعدادات' },
  'nav.permissions': { en: 'Permissions', ar: 'الصلاحيات' },
  'nav.profile': { en: 'Profile', ar: 'الملف الشخصي' },
  'nav.branches': { en: 'Branches', ar: 'الفروع' },

  // ---- Departments screen (already shipped; copy reviewed ----
  'departments.title': { en: 'Departments', ar: 'الأقسام' },
  'departments.subtitle': { en: 'Manage support departments and their managers.', ar: 'إدارة أقسام الدعم ومديريها.' },
  'departments.add': { en: 'Add department', ar: 'إضافة قسم' },
  'departments.create.title': { en: 'New department', ar: 'قسم جديد' },
  'departments.create.submit': { en: 'Create', ar: 'إنشاء' },
  'departments.list.title': { en: 'All departments', ar: 'كل الأقسام' },
  'departments.empty': { en: 'No departments yet.', ar: 'لا توجد أقسام بعد.' },
  'departments.state': { en: 'Status', ar: 'الحالة' },
  'departments.active': { en: 'Active', ar: 'نشط' },
  'departments.deactivated': { en: 'Deactivated', ar: 'معطّل' },
  'departments.actions': { en: 'Actions', ar: 'الإجراءات' },
  'departments.deactivate': { en: 'Deactivate', ar: 'تعطيل' },

  // ---- Shared field + state labels ----
  'field.name': { en: 'Name', ar: 'الاسم' },
  'branches.region': { en: 'Region', ar: 'المنطقة' },
  'branches.timezone': { en: 'Timezone', ar: 'المنطقة الزمنية' },
  'sidebar.expand': { en: 'Expand sidebar', ar: 'توسيع الشريط الجانبي' },
  'sidebar.collapse': { en: 'Collapse sidebar', ar: 'طي الشريط الجانبي' },
  'sidebar.menu': { en: 'Open menu', ar: 'فتح القائمة' },
  'sidebar.close': { en: 'Close menu', ar: 'إغلاق القائمة' },
  'auth.signOut': { en: 'Sign out', ar: 'تسجيل الخروج' },
  'auth.signIn': { en: 'Sign in', ar: 'تسجيل الدخول' },

  // ---- Portal ----
  'portal.nav.dashboard': { en: 'Home', ar: 'الرئيسية' },
  'portal.nav.submit': { en: 'Submit ticket', ar: 'تقديم تذكرة' },
  'portal.nav.tickets': { en: 'My tickets', ar: 'تذاكرى' },
  'portal.nav.kb': { en: 'Knowledge base', ar: 'قاعدة المعرفة' },
  'portal.submit.title': { en: 'Submit a new ticket', ar: 'تقديم تذكرة جديدة' },
  'portal.tickets.title': { en: 'Ticket', ar: 'تذكرة' },

  // … every remaining key in the 556-line file receives the same reviewed `ar:` value.
};
```

- [ ] **Step 2: Verify no raw key / no refetch**

Add (or confirm existing) the rendered-DOM and switch tests:

```ts
// frontend/projects/common/src/lib/i18n/locale.store.spec.ts — append
it('AC313_2_NoRawTranslationKeysInRenderedHtml', () => {
  // renders a keyed label and asserts the raw key string never reaches the DOM
  const store = TestBed.inject(LocaleStore);
  store.setLocale('ar');
  expect(store.t('nav.dashboard')).not.toMatch(/^nav\./); // resolved, not 'nav.dashboard'
});

it('AC313_1_LocaleSwitchDoesNotRefetch', () => {
  const store = TestBed.inject(LocaleStore);
  const http = TestBed.inject(HttpTestingController);
  store.setLocale('ar'); // no expectations queued: switching must not issue a request
  store.setLocale('en');
  expect(http.match(() => true)).toEqual([]); // zero outgoing requests
});
```

- [ ] **Step 3: Run tests to verify they pass**

Run: `cd frontend && npx ng test common --watch=false --include='**/translations.audit.spec.ts' --include='**/locale.store.spec.ts'`
Expected: PASS — every key non-empty, none copied from English, placeholders balanced, switch issues no
HTTP request.

- [ ] **Step 4: Commit (after native-speaker sign-off is recorded in the story evidence)**

```bash
git add frontend/projects/common/src/lib/i18n/translations.ts frontend/projects/common/src/lib/i18n/locale.store.spec.ts
git commit -m "feat(arabic): reviewed Arabic catalogue (US-313 T2)"
```

## Definition of done

`AC-313.1` (every visible key reviewed) satisfied by the full-file replacement + the non-empty /
not-copied audits, gated on recorded native-speaker sign-off (reviewer/date/scope in the story
evidence). `AC-313.2` (no identifier fallback, parity) satisfied by the parity audit, the placeholder
balance check, and the no-raw-key / no-refetch tests. Manual linguistic quality cannot be proven by
automation and is explicitly out of scope for the tests. Full gate:

```powershell
cd frontend
npx ng test common --watch=false
npx ng test admin-app --watch=false
npx ng test portal-app --watch=false
npx ng build admin-app
npx ng build portal-app
npx playwright test --grep "Arabic|language|locale"
```
