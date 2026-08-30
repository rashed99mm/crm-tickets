# US-314 Organisation Branding — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let an authorised administrator store safe organisation branding (logo URL, primary colour,
accent colour) and apply it at app startup — without exposing private platform settings, accepting
executable content, or letting one tenant read another's brand.

**Architecture:** A dedicated `Branding` feature on the backend (not the global `PlatformSettings`
table, which is keyed `Key`-only and therefore not tenant-scoped) and a shared `BrandStore` + `BrandingApi`
on the frontend that loads once per app bootstrap and writes CSS variables to `document.documentElement`
before the shell paints. The admin editor is an Admin-guarded lazy route reusing `CsCard`/`CsInputField`.

**Tech Stack:** Backend .NET 10 / EF Core / MediatR; Frontend Angular 20 standalone, signals,
`AsyncState`, reactive forms, `LocaleStore` (for `dir`, no mirroring of the logo).

**Spec:** `docs/superpowers/specs/EPIC-13-EPIC-13-US-314-branding.md`

**Not implemented this pass.** This plan is written ahead of any code that implements it, per explicit
instruction. **Tenant gate:** the contract below assumes a single-tenant deployment (the product's
current model has no tenant id in `IUserContext`). If a multi-tenant model appears, this is an ADR/blocker
and `GET/PUT /api/branding` must gain a tenant scope before Task 1 — `BranchId` is organisational
grouping and must NOT become tenant id.

---

## Global Constraints

- Validate `#RRGGBB` only (no `rgb()`, no named colours), absolute HTTPS logo URLs with an allowed image
  extension, and length limits. Reject HTML/SVG-with-script/CSS/arbitrary style text and any client-
  supplied tenant id. `PUT` is Admin-protected; the read returns only `logoUrl`/`primaryColor`/
  `accentColor`.
- The frontend never calls `bypassSecurityTrustHtml`. The logo is a plain `<img [src]>` with an escaped
  URL and meaningful `alt` (or `alt=""` when decorative). On load failure or missing data the store
  retains the `theme.css` safe defaults — branding is enhancement, never a hard dependency.

---

### Task 1: Backend branding feature (`AC-314.1`, `AC-314.2`, `AC-314.3`)

**Files:**
- Create: `backend/src/CustomerSupport.Domain/Entities/PlatformSettings/BrandingSettings.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Branding/Dtos/BrandingDtos.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Branding/Queries/GetBranding/`
- Create: `backend/src/CustomerSupport.Application/Features/Branding/Commands/UpdateBranding/`
- Create: `backend/src/CustomerSupport.InternalApi/Controllers/BrandingController.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/BrandingSettingsConfiguration.cs` + migration
- Test: `backend/tests/CustomerSupport.Tests/Integration/BrandingEndpointTests.cs`

**Interfaces:**
- Produces: `BrandingSettings(Guid Id, string LogoUrl, string PrimaryColor, string AccentColor,
  bool IsActive)` (single-row table; the first row is "the" brand). `BrandingDto(string LogoUrl,
  string PrimaryColor, string AccentColor)`. `UpdateBrandingCommand(string LogoUrl, string
  PrimaryColor, string AccentColor) : ICommand<Response<BrandingDto>>`.

- [ ] **Step 1: Write the failing test**

```csharp
// backend/tests/CustomerSupport.Tests/Integration/BrandingEndpointTests.cs
[Fact] [Trait("AC", "314.1")]
public async Task AC314_1_GetBrandingReturnsCurrentTenant()
{
    var client = (await _factory.CreateAuthenticatedClientAsync(admin: true)).Item1;
    var response = await client.GetFromJsonAsync<Response<BrandingDto>>("/api/branding");
    response!.Success.Should().BeTrue();
    response.Data!.PrimaryColor.Should().MatchRegex("^#[0-9A-Fa-f]{6}$");
}

[Fact] [Trait("AC", "314.1")]
public async Task AC314_1_UpdateBrandingRequiresAdmin()
{
    var client = (await _factory.CreateAuthenticatedClientAsync(admin: false)).Item1;
    var response = await client.PutAsJsonAsync("/api/branding", new { logoUrl="https://x.test/logo.svg", primaryColor="#2457A6", accentColor="#F2A900" });
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}

[Fact] [Trait("AC", "314.2")]
public async Task AC314_2_InvalidColorUrlAndAssetAreRejected()
{
    var client = (await _factory.CreateAuthenticatedClientAsync(admin: true)).Item1;
    var response = await client.PutAsJsonAsync("/api/branding", new { logoUrl="javascript:alert(1)", primaryColor="blue", accentColor="#F2A900" });
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

- [ ] **Step 2: Run test to verify it fails** — routes don't exist.

- [ ] **Step 3: Implement**

```csharp
// backend/src/CustomerSupport.Application/Features/Branding/Dtos/BrandingDtos.cs
public record BrandingDto(string LogoUrl, string PrimaryColor, string AccentColor);
public record UpdateBrandingCommand(string LogoUrl, string PrimaryColor, string AccentColor)
    : ICommand<Response<BrandingDto>>;

// Handler validates and upserts the single branding row:
//   guard LogoUrl with Uri.IsWellFormedUriString + https + .svg/.png/.jpg extension + length;
//   guard colors with ^#[0-9A-Fa-f]{6}$ ; map failures to ApplicationErrors.Branding.INVALID_VALUE (400).
//   reject any field carrying '<' or 'script' (no HTML/SVG exec).
// GET returns the row or the safe defaults if none exists yet (AC-314.3 default survives).
```

`BrandingController.cs`: `[HttpGet("branding")]` (anonymous read is acceptable per approved contract —
fields are safe), `[HttpPut("branding")]` with `[Authorize(Policy = "Admin")]`.

- [ ] **Step 4: Run test to verify it passes** — `dotnet test … --filter "FullyQualifiedName~BrandingEndpointTests"`.

- [ ] **Step 5: Commit** the backend feature.

---

### Task 2: Frontend `BrandingApi` + `BrandStore` (`AC-314.3`)

**Files:**
- Create: `frontend/projects/common/src/lib/branding/branding.api.ts`
- Create: `frontend/projects/common/src/lib/branding/branding.api.spec.ts`
- Create: `frontend/projects/common/src/lib/branding/brand.store.ts`
- Create: `frontend/projects/common/src/lib/branding/brand.store.spec.ts`
- Modify: `frontend/projects/common/src/public-api.ts`
- Modify: `frontend/projects/common/src/styles/theme.css` (safe defaults)

**Interfaces:**
- Consumes: `GET /api/branding`, `PUT /api/branding` (backend Task 1).

- [ ] **Step 1: Write the failing tests**

```ts
// frontend/projects/common/src/lib/branding/brand.store.spec.ts
import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { envelopeInterceptor } from 'common';
import { BrandStore } from './brand.store';

function ok<T>(data: T) {
  return { success: true, code: 'CON035', message: 'OK', data, errors: [] };
}

describe('BrandStore (AC-314.3)', () => {
  let store: BrandStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    store = TestBed.inject(BrandStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('AC314_3_BrandStoreAppliesCssVariables', () => {
    store.load();
    http.expectOne('/api/branding').flush(
      ok({ logoUrl: 'https://cdn.test/logo.svg', primaryColor: '#2457A6', accentColor: '#F2A900' }),
    );

    expect(document.documentElement.style.getPropertyValue('--brand-primary')).toBe('#2457A6');
    expect(document.documentElement.style.getPropertyValue('--brand-accent')).toBe('#F2A900');
    expect(store.logoUrl()).toBe('https://cdn.test/logo.svg');
  });

  it('AC314_3_DefaultBrandingSurvivesLoadFailure', () => {
    store.load();
    http.expectOne('/api/branding').error(new ErrorEvent('network'));

    expect(store.primaryColor()).toBe('#1f3a8a'); // theme.css default
    expect(document.documentElement.style.getPropertyValue('--brand-primary')).toBe('');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx ng test common --watch=false --include='**/brand.store.spec.ts'`
Expected: FAIL — `BrandStore` doesn't exist.

- [ ] **Step 3: Implement `BrandingApi` + `BrandStore`**

```ts
// frontend/projects/common/src/lib/branding/branding.api.ts
import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface Branding {
  readonly logoUrl: string;
  readonly primaryColor: string;
  readonly accentColor: string;
}

@Injectable({ providedIn: 'root' })
export class BrandingApi {
  private readonly http = inject(HttpClient);
  get(): Observable<Branding> {
    return this.http.get<Branding>('/api/branding');
  }
  update(request: Branding): Observable<Branding> {
    return this.http.put<Branding>('/api/branding', request);
  }
}
```

```ts
// frontend/projects/common/src/lib/branding/brand.store.ts
import { Injectable, signal } from '@angular/core';
import { ApiError, AsyncState, Branding, BrandingApi, failed, loaded } from 'common';

const DEFAULT_PRIMARY = '#1f3a8a';
const DEFAULT_ACCENT = '#f2a900';

/**
 * Loads the org brand once per app bootstrap and writes it to CSS variables on
 * <html>. On failure it silently keeps the theme.css defaults — branding is an
 * enhancement, never a hard dependency (AC-314.3).
 */
@Injectable({ providedIn: 'root' })
export class BrandStore {
  private readonly api = inject(BrandingApi);

  readonly logoUrl = signal('');
  readonly primaryColor = signal(DEFAULT_PRIMARY);
  readonly accentColor = signal(DEFAULT_ACCENT);
  readonly status = signal<AsyncState<Branding>>(loaded({ logoUrl: '', primaryColor: DEFAULT_PRIMARY, accentColor: DEFAULT_ACCENT }));

  load(): void {
    this.api.get().subscribe({
      next: (b) => {
        this.apply(b);
        this.status.set(loaded(b));
      },
      error: () => {
        this.apply({ logoUrl: '', primaryColor: DEFAULT_PRIMARY, accentColor: DEFAULT_ACCENT });
        this.status.set(failed(new ApiError('ERR_BRANDING', 'Branding unavailable', [], '', 0)));
      },
    });
  }

  /** Admin editor calls this after a successful PUT (AC-314.1/314.2). */
  applyAndPersist(b: Branding): void {
    this.apply(b);
    this.status.set(loaded(b));
  }

  private apply(b: Branding): void {
    this.logoUrl.set(b.logoUrl);
    this.primaryColor.set(b.primaryColor);
    this.accentColor.set(b.accentColor);
    const root = document.documentElement.style;
    root.setProperty('--brand-primary', b.primaryColor);
    root.setProperty('--brand-accent', b.accentColor);
  }
}
```

- [ ] **Step 4: Add safe defaults to `theme.css`**

```css
:root {
  --brand-primary: #1f3a8a;
  --brand-accent: #f2a900;
}
```

Export from `public-api.ts`: `export * from './lib/branding/branding.api';` and `./lib/branding/brand.store';`.

- [ ] **Step 5: Run test to verify it passes**

Run: `cd frontend && npx ng test common --watch=false --include='**/brand.store.spec.ts' --include='**/branding.api.spec.ts'`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add frontend/projects/common/src/lib/branding/ frontend/projects/common/src/public-api.ts frontend/projects/common/src/styles/theme.css
git commit -m "feat(branding): BrandingApi + BrandStore apply CSS vars (US-314 T2)"
```

---

### Task 3: Admin editor + bootstrap + tenant isolation (`AC-314.1`, `AC-314.2`, `AC-314.3`)

**Files:**
- Create: `frontend/projects/admin-app/src/app/features/admin/branding.component.ts`
- Create: `frontend/projects/admin-app/src/app/features/admin/branding.component.html`
- Create: `frontend/projects/admin-app/src/app/features/admin/branding.component.spec.ts`
- Modify: `frontend/projects/admin-app/src/app/app.routes.ts`
- Modify: `frontend/projects/admin-app/src/app/layout/shell.component.ts`
- Modify: `frontend/projects/common/src/lib/i18n/translations.ts`
- Modify: `frontend/projects/admin-app/src/main.ts` (bootstrap calls `BrandStore.load()`)

**Interfaces:**
- Consumes: `BrandStore`, `BrandingApi`, `AsyncState`, `CsCard`/`CsInputField`/`CsButton`/`CsDialog`.

- [ ] **Step 1: Write the failing editor test**

```ts
// frontend/projects/admin-app/src/app/features/admin/branding.component.spec.ts
it('AC314_2: rejects an invalid colour via server error mapped to the control', () => {
  const fixture = TestBed.createComponent(BrandingComponent);
  fixture.detectChanges();
  http.expectOne('/api/branding').flush(ok(defaults));

  fixture.componentInstance.form.setValue({ logoUrl: 'x', primaryColor: 'blue', accentColor: '#F2A900' });
  fixture.componentInstance.save();
  http.expectOne((r) => r.url === '/api/branding' && r.method === 'PUT')
    .flush({ success:false, code:'BRANDING_INVALID', message:'Invalid', data:null,
             errors:[{ field:'PrimaryColor', code:'ERR', message:'Must be #RRGGBB' }] });
  fixture.detectChanges();

  expect(fixture.componentInstance.fieldError('primaryColor')?.message).toContain('#RRGGBB');
});
```

- [ ] **Step 2: Implement the editor**

```ts
// frontend/projects/admin-app/src/app/features/admin/branding.component.ts (excerpt)
readonly form = new FormGroup({
  logoUrl: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  primaryColor: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^#[0-9A-Fa-f]{6}$/)] }),
  accentColor: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^#[0-9A-Fa-f]{6}$/)] }),
});

save(): void {
  if (this.form.invalid || this.saving()) return;
  this.saving.set(true);
  this.api.update(this.form.getRawValue()).subscribe({
    next: (b) => { this.saving.set(false); this.brand.applyAndPersist(b); this.saved.set(true); },
    error: (e) => { this.saving.set(false); this.error.set(this.toApiError(e)); },
  });
}
```

Template renders a preview block using the CSS variables (`bg-[var(--brand-primary)]`, an `<img
[src]="brand.logoUrl()" [alt]="'branding.logoAlt' | t">`), the reactive form with `CsInputField`
`[serverError]="fieldError('primaryColor')"`, and a success `cs-badge`. No `bypassSecurityTrustHtml`.

In `app.routes.ts` add, after `settings`:
```ts
      { path: 'branding', canActivate: [roleGuard('Admin')],
        loadComponent: () => import('./features/admin/branding.component') },
```
In `shell.component.ts` `NAV_ITEMS` add `{ path: '/branding', key: 'nav.branding', icon: 'palette', adminOnly: true }`.
In `translations.ts` add `'nav.branding'`, `'branding.title'`, `'branding.logo'`, `'branding.primary'`,
`'branding.accent'`, `'branding.save'`, `'branding.logoAlt'`, `'branding.saved'` (en/ar pairs).

In `main.ts` (admin + portal) call `inject(BrandStore).load()` before `bootstrapApplication` completes
so variables are set before first paint.

- [ ] **Step 3: Run tests to verify they pass**

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/branding.component.spec.ts'`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/admin/branding.component.ts frontend/projects/admin-app/src/app/features/admin/branding.component.html frontend/projects/admin-app/src/app/features/admin/branding.component.spec.ts frontend/projects/admin-app/src/app/app.routes.ts frontend/projects/admin-app/src/app/layout/shell.component.ts frontend/projects/common/src/lib/i18n/translations.ts frontend/projects/admin-app/src/main.ts
git commit -m "feat(branding): admin editor + bootstrap wiring (US-314 T3)"
```

## Definition of done

`AC-314.1` (authorized store/retrieve, tenant scope) via backend Task 1 + admin-only route. `AC-314.2`
(invalid values/assets rejected, mapped to controls) via backend validation + `fieldError` mapping.
`AC-314.3` (safe defaults, CSS-var application, isolation, accessible logo) via `BrandStore` defaults +
editor test + cross-tenant backend test. Full gate:

```powershell
cd backend; dotnet test CustomerSupport.slnx; dotnet build CustomerSupport.slnx --warnaserror
cd ..\frontend
npx ng test common --watch=false; npx ng test admin-app --watch=false; npx ng test portal-app --watch=false
npx ng build admin-app; npx ng build portal-app
npx playwright test --grep "branding|logo"
```
