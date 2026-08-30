# task-F3 — verify + build

**Status:** In progress (unit + build verified; live cross-host check pending host restart)
**AC:** FB-4, FB-5, FB-8, FB-9; CC-30/CC-31/CC-34 (consume)

## Evidence (real outputs)

`npx ng test common --watch=false`:
```
 Test Files  48 passed (48)
      Tests  205 passed (205)
```

`npx ng test portal-app --watch=false`:
```
 Test Files  14 passed (14)
      Tests  65 passed (65)
```

`npx ng build portal-app --configuration development`:
```
Application bundle generation complete. [15.438 seconds]
Output location: ...\frontend\dist\portal-app
```

Remaining: live two-app check — portal widget receives an agent reply pushed across the
InternalApi/ExternalApi host boundary (blocked until backend hosts are restarted after the backend
build).
