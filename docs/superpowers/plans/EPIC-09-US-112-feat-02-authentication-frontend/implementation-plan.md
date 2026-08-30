# FEAT-02 Authentication (frontend) Implementation Plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

**Goal:** The Angular sign-in screen, the typed `AuthApi`, the envelope/auth/refresh interceptor chain, the `SessionStore`, and the route guards — the client half of `FEAT-02` (`AC-55`..`AC-56`, `FE-9`..`FE-11`).

**Architecture:** `admin-app` consumes the `common` library. Interceptors are registered once in `app.config.ts` in a deliberate order. The envelope interceptor is the *only* place that knows the response envelope exists (`FE-4`).

## Global constraints

- No component reads `ApiEnvelope` fields directly — only `envelope.interceptor.ts`.
- The refresh interceptor is single-flight (`RefreshCoordinator`) so concurrent 401s share one refresh (`FE-11`).
- `localStorage` holds the session (page refresh must not sign out); XSS trade-off recorded in the spec.

## Task 1 — `AuthApi` + envelope contract (`FE-9`)

**Files:**
- `frontend/projects/common/src/lib/auth/auth.api.ts`
- `frontend/projects/common/src/lib/api/{api-response,api-error,envelope.interceptor}.ts`

**Interfaces:** `AuthApi.signIn(email, password): Observable<AuthResponse>`; `AuthResponse` matches the backend's `AuthResponse` record field-for-field.

**Step 1 — Real service + envelope (excerpt)**

```ts
// frontend/projects/common/src/lib/auth/auth.api.ts
@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  signIn(email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/Auth/login', { email, password });
  }
  refresh(accessToken: string, refreshToken: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/Auth/refresh', { accessToken, refreshToken });
  }
}
```

```ts
// frontend/projects/common/src/lib/api/envelope.interceptor.ts
export const envelopeInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    map((event) => event instanceof HttpResponse && isApiEnvelope(event.body)
      ? event.clone({ body: (event.body as ApiEnvelope<unknown>).data }) : event),
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && isApiEnvelope(error.error)) {
        const env = error.error as ApiEnvelope<unknown>;
        return throwError(() => new ApiError(env.code, env.message,
          (env.errors ?? []).map((e) => ({ field: toControlName(e.field), code: e.code, message: e.message })),
          env.traceId ?? '', error.status));
      }
      return throwError(() => new ApiError('ERR_NETWORK', 'Could not reach the server', [], '', error.status));
    }),
  );
```

- [ ] **Step 2: Run:** `cd frontend && npx ng test common --watch=false --filter envelope`
Expected: PASS — success unwraps to `data`; failure → `ApiError` with lowercased field; bare 502 → displayable `ApiError`.

- [ ] **Step 3: Commit:** `git add frontend/projects/common/src/lib/auth/auth.api.ts frontend/projects/common/src/lib/api/ && git commit -m "feat(auth-fe): AuthApi + envelope (FE-9)"`

## Task 2 — `SessionStore`, `TokenStorage`, `auth`/`refresh` interceptors (`FE-10`, `FE-11`)

**Files:**
- `frontend/projects/common/src/lib/auth/{session.store,token-storage,auth.interceptor,refresh.interceptor}.ts`

**Step 1 — Real interceptors**

```ts
// auth.interceptor.ts
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(SessionStore).token();
  return !token ? next(req)
    : next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};

// refresh.interceptor.ts (single-flight via RefreshCoordinator)
export const refreshInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.url === '/api/Auth/refresh') return next(req);
  const coordinator = inject(RefreshCoordinator);
  return next(req).pipe(catchError((error: unknown) => {
    if (!(error instanceof ApiError) || error.status !== 401) return throwError(() => error);
    return coordinator.refresh().pipe(switchMap(() => next(req)));
  }));
};
```

The `SessionStore` reads the JWT's role claim (ASP.NET role URI) to derive `roles()` and `isAuthenticated`, but renders `displayName` from the stored `AuthResponse` (the access token carries no `name` claim). Hiding a button is not authorization (`AC-61`).

- [ ] **Step 2: Run:** `cd frontend && npx ng test common --watch=false --filter "refresh|session"`
Expected: PASS — 401 triggers one shared refresh, retried request carries new token; failed refresh clears session.

- [ ] **Step 3: Commit:** `git add frontend/projects/common/src/lib/auth/ && git commit -m "feat(auth-fe): session + single-flight refresh (FE-10, FE-11)"`

## Task 3 — Login component + guards + provider wiring (`AC-55`, `AC-56`)

**Files:**
- `frontend/projects/admin-app/src/app/features/auth/login.component.ts`
- `frontend/projects/common/src/lib/auth/guards.ts`
- `frontend/projects/admin-app/src/app/app.config.ts`

**Step 1 — Real login component (excerpt)**

```ts
export default class LoginComponent {
  readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });
  readonly busy = signal(false);
  readonly error = signal<ApiError | null>(null);

  submit(): void {
    if (this.form.invalid || this.busy()) return;
    this.busy.set(true); this.error.set(null);
    const { email, password } = this.form.getRawValue();
    this.api.signIn(email, password).subscribe({
      next: (result) => { this.session.signIn(result); this.busy.set(false); void this.router.navigateByUrl(this.returnUrl()); },
      error: (failure: unknown) => this.error.set(failure instanceof ApiError ? failure
        : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0)),
    });
  }
}
```

`authGuard` redirects an unauthenticated visit to `/login` carrying `returnUrl`; `roleGuard('Admin')` (and later `roleGuard('Supervisor','Admin')`) is a courtesy — the endpoint policy is the control.

**Step 2 — Real provider order (app.config.ts)**

```ts
provideHttpClient(withInterceptors([refreshInterceptor, authInterceptor, envelopeInterceptor])),
```

Inbound order matters: refresh reacts to `ApiError` (401) first, then auth attaches the token, then envelope converts the body.

- [ ] **Step 3: Run:** `cd frontend && npx ng test admin-app --watch=false --filter login`
Expected: PASS — invalid creds show `ApiError`; success navigates to `returnUrl`.

- [ ] **Step 4: Commit:** `git add frontend/projects/admin-app/src/app/features/auth/ frontend/projects/common/src/lib/auth/guards.ts frontend/projects/admin-app/src/app/app.config.ts && git commit -m "feat(auth-fe): login screen + guards + provider order (AC-55, AC-56)"`

## Self-review

Coverage: `FE-9` → Task 1; `FE-10`,`FE-11` → Task 2; `AC-55`,`AC-56` → Task 3.

**Discrepancy found:** the old plan listed `roleGuard('Supervisor')` as the staff guard. The shipped `users` route uses `roleGuard('Admin')`; `Supervisor` appears only on reports routes as `roleGuard('Supervisor','Admin')` (AC-164 addendum). No contradiction in the rewrite — matches the real `app.routes.ts`.
