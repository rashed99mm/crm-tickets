# T9 — Full build + test verification

**AC:** AC-R7, AC-R8, AC-R9
**Status:** pending

## What this task does

Runs all builds and tests to verify no regressions. Pastes actual command output as evidence.

## Verification commands

```
cd backend && dotnet build CustomerSupport.slnx
cd backend && dotnet test CustomerSupport.slnx
cd frontend && npx ng build admin-app
```

## Evidence

(Output pasted here after execution)
