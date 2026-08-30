# T7 — Parameterize InfrastructureExtensions.AppName per host

**AC:** AC-R6
**Status:** done — `AddPlatformInfrastructureServices` takes `string appName` and passes it to `resource.AddService(appName)`.

## What this task does

Replaces the hardcoded `private const string AppName = "CustomerSupport.InternalApi"` in `InfrastructureExtensions.cs` with a parameter so each host can supply its own name. InternalApi passes `"CustomerSupport.InternalApi"`, ExternalApi passes `"CustomerSupport.ExternalApi"`.

## Files to modify

- `backend/src/CustomerSupport.Api.Shared/Extensions/InfrastructureExtensions.cs` — change method signature to accept `string appName`
- `backend/src/CustomerSupport.InternalApi/Program.cs` (or composition root) — pass `"CustomerSupport.InternalApi"`
- `backend/src/CustomerSupport.ExternalApi/Program.cs` (or composition root) — pass `"CustomerSupport.ExternalApi"`

## Verification

`dotnet build` succeeds. Both hosts report distinct names in telemetry config.
