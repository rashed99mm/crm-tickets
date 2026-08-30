# Frontend Foundation Implementation Plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

**Goal:** The Angular 20 workspace — the `common` library (API layer, auth, i18n, UI primitives, state), the `admin-app` host, and the `portal-app` host — plus the interceptor chain, the session, and the router that every feature screen builds on (`FE-1`..`FE-12`).

**Architecture:** `frontend/` is an Angular workspace: `projects/common` (shared library, imported as the `common` barrel), `projects/admin-app` (staff host), `projects/portal-app` (customer host). Standalone components, signals, zoneless-ready. The `common` barrel `public-api` re-exports the interceptors, stores, API services and UI components.

## Global constraints

- One place knows the envelope exists: `envelope.interceptor.ts` (`FE-4`). No component reads `ApiEnvelope`.
- Interceptor order is load-bearing: `refresh → auth → envelope` (inbound: refresh sees `ApiError` first, then token attaches, then body unwraps).
- `localStorage` session; XSS trade-off recorded in spec. RTL + bilingual handled by `LocaleStore`/`translate.pipe`, not hardcoded strings (`no-hardcoded-strings.spec.ts` guards it).

## Task 1 — Workspace + `common` library surface (`FE-1`)

**Files:**
- `frontend/angular.json`, `frontend/projects/common/src/public-api.ts`
- `frontend/projects/common/src/lib/api/{api-response,api-error}.ts`

**Interfaces:** `ApiEnvelope<T>`, `FieldError`, `ApiError`, `PagedResult<T>`.

**Step 1 — Real envelope types (excerpt)**

```ts
// frontend/projects/common/src/lib/api/api-response.ts
export interface ApiEnvelope<T> {
  readonly success: boolean; readonly code: string; readonly message: string;
  readonly data: T | null; readonly errors: readonly FieldError[];
  readonly traceId?: string; readonly timestamp?: string;
}
export function isApiEnvelope(body: unknown): body is ApiEnvelope<unknown> {
  return typeof body === 'object' && body !== null
    && 'success' in body && 'data' in body && 'code' in body;
}
```

```ts
// frontend/projects/common/src/lib/api/api-error.ts
export class ApiError extends Error {
  constructor(readonly code: string, readonly message_: string,
    readonly errors: readonly FieldError[], readonly traceId: string, readonly status: number) {
    super(`${code}: ${message_}`); this.name = 'ApiError';
  }
  fieldError(field: string): FieldError | undefined {
    return this.errors.find((e) => e.field === field);
  }
  get hasFieldErrors(): boolean { return this.errors.length > 0; }
}
```

- [ ] **Step 2: Run:** `cd frontend && npx ng build common`
Expected: clean build; the `common` barrel compiles, exported to `admin-app`/`portal-app`.

- [ ] **Step 3: Commit:** `git add frontend/projects/common/ && git commit -m "feat(fe-foundation): common lib, envelope types (FE-1, FE-4)"`

## Task 2 — Interceptor chain + session (`FE-9`, `FE-10`, `FE-11`)

**Files:**
- `frontend/projects/common/src/lib/api/envelope.interceptor.ts`
- `frontend/projects/common/src/lib/auth/{auth.interceptor,refresh.interceptor,session.store,token-storage}.ts`

**Step 1 — Real chain (excerpt)**

```ts
// auth.interceptor.ts
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(SessionStore).token();
  return !token ? next(req) : next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
// refresh.interceptor.ts — single-flight via RefreshCoordinator
export const refreshInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.url === '/api/Auth/refresh') return next(req);
  const coordinator = inject(RefreshCoordinator);
  return next(req).pipe(catchError((error: unknown) => {
    if (!(error instanceof ApiError) || error.status !== 401) return throwError(() => error);
    return coordinator.refresh().pipe(switchMap(() => next(req)));
  }));
};
```

`SessionStore` derives `isAuthenticated`/`roles` from the JWT's role claim (ASP.NET role URI) and `displayName` from the stored `AuthResponse` (the access token has no `name` claim). `TokenStorage` isolates `localStorage` access, guarded against private-mode throws.

- [ ] **Step 2: Run:** `cd frontend && npx ng test common --watch=false --filter "envelope|refresh|session"`
Expected: PASS — envelope unwrap, single-flight refresh, session derive.

- [ ] **Step 3: Commit:** `git add frontend/projects/common/src/lib/api/envelope.interceptor.ts frontend/projects/common/src/lib/auth/ && git commit -m "feat(fe-foundation): interceptor chain + session (FE-9..FE-11)"`

## Task 3 — Routing + provider wiring (`FE-5`, `FE-12`)

**Files:**
- `frontend/projects/admin-app/src/app/app.config.ts`
- `frontend/projects/admin-app/src/app/app.routes.ts`
- `frontend/projects/common/src/lib/auth/guards.ts`

**Step 1 — Real provider order (app.config.ts)**

```ts
export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([refreshInterceptor, authInterceptor, envelopeInterceptor])),
  ],
};
```

**Step 2 — Real guards + route tree (excerpt)**

```ts
export const authGuard: CanActivateFn = (_route, state) => {
  const session = inject(SessionStore); const router = inject(Router);
  return session.isAuthenticated() ? true
    : router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};
export function roleGuard(...roles: readonly string[]): CanActivateFn {
  return (_route, state) => {
    const session = inject(SessionStore); const router = inject(Router);
    if (!session.isAuthenticated()) return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
    return roles.some((r) => session.hasRole(r)) ? true : router.createUrlTree(['/forbidden']);
  };
}
```

`app.routes.ts` puts `authGuard` on the shell parent so every child is protected by default; `login` is the only anonymous route; `tickets/new` is declared **before** `tickets/:id` so `new` is not captured as an id; `users`/`departments`/`sla-policies`/`settings`/`permissions` carry `roleGuard('Admin')`; reports carry `roleGuard('Supervisor','Admin')`.

- [ ] **Step 3: Run:** `cd frontend && npx ng test admin-app --watch=false --filter routes`
Expected: PASS — anonymous hits `/tickets` → redirected to `/login?returnUrl=/tickets`; `Admin` reaches `/users`; non-admin → `/forbidden`.

- [ ] **Step 4: Commit:** `git add frontend/projects/admin-app/src/app/app.config.ts frontend/projects/admin-app/src/app/app.routes.ts frontend/projects/common/src/lib/auth/guards.ts && git commit -m "feat(fe-foundation): routes + providers (FE-5, FE-12)"`

## Task 4 — UI primitives + state union (`FE-6`, `FE-7`)

**Files:** `frontend/projects/common/src/lib/ui/*` (`button`, `input-field`, `card`, `status-pill`, `loading-state`, `empty-state`, `error-state`, `badge`, `dialog`), `frontend/projects/common/src/lib/state/async-state.ts`.

**Step 1 — Real state union**

```ts
// frontend/projects/common/src/lib/state/async-state.ts
export type AsyncState<T> =
  | { status: 'loading' } | { status: 'empty' } | { status: 'loaded'; data: T } | { status: 'error'; error: ApiError };
export const loading = <T>(): AsyncState<T> => ({ status: 'loading' });
export const empty = <T>(): AsyncState<T> => ({ status: 'empty' });
export const loaded = <T>(data: T): AsyncState<T> => ({ status: 'loaded', data });
export const failed = <T>(error: ApiError): AsyncState<T> => ({ status: 'error', error });
```

The queue and users components consume this union — the mechanism behind `AC-58`/`AUTH-18` distinct states.

- [ ] **Step 2: Run:** `cd frontend && npx ng test common --watch=false --filter "ui|async-state"`
Expected: PASS — every UI primitive renders; `AsyncState` constructors typed.

- [ ] **Step 3: Commit:** `git add frontend/projects/common/src/lib/ui/ frontend/projects/common/src/lib/state/ && git commit -m "feat(fe-foundation): UI primitives + AsyncState (FE-6, FE-7)"`

## Self-review

Coverage: `FE-1`,`FE-4` → Task 1; `FE-9`..`FE-11` → Task 2; `FE-5`,`FE-12` → Task 3; `FE-6`,`FE-7` → Task 4.

**Discrepancy found:** the old 1929-line foundation narrative described hand-rolled stores and a different interceptor set. The shipped code is exactly the envelope/session/refresh chain above; the rewrite quotes the real `app.config.ts` provider order and the real `AsyncState` union rather than the prose's implied custom state service. The `portal-app` host exists in the workspace but its customer-facing screens are a later slice — the foundation only establishes the workspace and shared lib.
