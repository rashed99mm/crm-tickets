# Cross-Host Live-Chat Delivery — plan record

**Spec:** `EPIC-12-US-000-cross-host-live-chat-delivery-addendum.md` (CC-30..CC-34)
**Date:** 2026-08-29
**Status:** Backend implemented (16/16 chat tests pass); frontend F1/F2/F3 unit+build verified
(common 205/205, portal 65/65, portal-app build clean). Remaining: restart backend hosts + live
cross-host widget check; per user directive run one backend test (not the full suite) after.

## Frontend phase
Frontend plan: `EPIC-12-US-000-cross-host-live-chat-delivery-frontend.md` (in this folder). It wires the
anonymous `/hubs/chat` SignalR client so the portal widget renders agent replies as they arrive and
harness the new cross-host pump.

## Criteria delivered
- CC-30 agent→customer cross-host real-time delivery (MassTransit pump)
- CC-31 session-scoped delivery
- CC-32 idempotent delivery on duplicate publish
- CC-33 graceful degradation with no bus (persist + transcript)
- CC-34 customer→agent delivery via pump

## Deviations from spec (recorded)
- **Single-source push:** the spec draft said keep the direct push + publish, with the consumer on
  both hosts. Analysis showed that double-delivers to staff on `/hubs/main` (handler push + consumer
  push). The plan makes the consumer the single source of the real-time push; handlers publish instead
  of pushing directly. Persisted-before-publish is preserved.

## Tasks
- task-01-shared-contract.md
- task-02-publish-from-handlers.md
- task-03-consumer-and-registration.md
- task-04-integration-tests.md
- task-F1-realtime-client.md
- task-F2-widget-live-receive.md
- task-F3-verify-build.md

## Gaps accepted
- No SignalR backplane; each host pushes to its own local connections (event carried by the bus).
- With `NoOpMessagePublisher`, no real-time push (transcript fallback), matching email/SMS behaviour.
