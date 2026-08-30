# Task 04 — Contract hardening sweep

## Traceability
Epic:   docs/requirements/epics/EPIC-09-administration.md (cross-cutting obligation)
Stories: US-122-stable-code-per-condition.md, US-123-diagnosable-without-leaking.md,
         US-124-unambiguous-wire-format.md
FEAT:   FEAT-09 (Contract hardening) — delivery-plan.md, sprint 4
Plan:   docs/superpowers/plans/EPIC-12-US-122-contract-hardening/

## Work
EveryErrorCode_HasABilingualMessage is red. Every code needs all THREE wires:
SystemCode.cs, SystemCodeMap.cs, Resources.yaml (en + ar). This stays a standing gate: every
later task that adds an error code (S1/S2/S3) must keep this test green.

## Gate
dotnet test --filter "FullyQualifiedName~ContractHardeningTests" → green, output pasted.
