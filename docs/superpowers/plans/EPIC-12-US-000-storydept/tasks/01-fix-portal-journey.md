# Task 01 — Fix portal journey backend (PJ2/PJ3/PJ5/PJ8–12, ASG8 ×2)

## Traceability
Epic:   docs/requirements/epics/EPIC-07-customer-portal.md
Stories: docs/requirements/user-stories/US-401-customer-registration.md,
         US-402-customer-login.md, US-403-customer-authorization.md,
         US-405-portal-my-tickets.md, US-406-portal-ticket-detail.md, US-407-portal-reply.md
FEAT:   FEAT-22 (Customer portal) — delivery-plan.md row 10
Spec:   docs/superpowers/specs/EPIC-07-US-404-portal-home-and-signup-design.md
Plan:   docs/superpowers/plans/EPIC-07-US-404-portal-home-and-signup/

## Work
Root cause unknown — diagnose first, two suspects:
(a) registration command/handler drops `PhoneNumber` (ASG8 asserts it persists);
(b) token factory omits the `customerId` claim portal endpoints authorize on (PJ3).
Diagnostic entry points: Application/Features/Auth|Portal handlers, token creation in
Infrastructure, the failing assertions in PortalJourneyEndpointTests.cs.

Token claim pattern (if (b)):
```csharp
if (customerId is not null)
    claims.Add(new Claim("customerId", customerId.Value.ToString()));
```

## Tests (already exist and are red — make them green, do not weaken)
PortalJourneyEndpointTests.PJ2/PJ3/PJ5/PJ8_9_10_12, PJ9_*, PJ11, PJ12
PortalRegisterEndpointTests.ASG8_Register_PersistsPhoneNumber, ASG8_Register_BlankPhone_StaysNull

## Gate
dotnet test --filter "FullyQualifiedName~PortalJourney|FullyQualifiedName~PortalRegister"
→ green, output pasted.
