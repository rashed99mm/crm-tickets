# Task 06 — Notification Evidence

**Criteria:** `NG-1` through `NG-8`

## Steps

1. Run focused unit tests with fake HTTP handlers and fake SignalR publisher.
2. Run integration tests through the real API pipeline using test configuration without credentials.
3. Assert no response or log contains API keys, passwords, OTPs, bodies, or provider internals.
4. Run the full backend build/test gate and paste actual output into the story records.

**Run:** `dotnet build backend/CustomerSupport.slnx --warnaserror`  
**Expected:** Build succeeds with zero warnings; focused and full tests pass.

**Commit:** `test: evidence notification gateway behavior`
