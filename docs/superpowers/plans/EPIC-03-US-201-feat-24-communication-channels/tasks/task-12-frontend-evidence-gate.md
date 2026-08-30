# Task 12 — Frontend Evidence Gate

**Criteria:** all frontend criteria `FB-1`..`FB-9`  
**Status:** pending  
**Commit:** none

## Steps

1. Review changed frontend files for inline classes, duplicated API contracts, hardcoded strings,
   accidental AI-chat reuse, and incorrect anonymous/authenticated route placement.
2. Run the focused tests for Tasks 07-11.
3. Run the complete common, admin-app, and portal-app suites.
4. Build both apps and record actual output.
5. Manually verify the admin queue/transcript and anonymous portal routes against their correct hosts.
6. Update this task, the frontend plan, the plan README, and delivery-plan status from observed output.

## Commands

```text
cd frontend
npx ng test common --watch=false
npx ng test admin-app --watch=false
npx ng test portal-app --watch=false
npx ng build admin-app
npx ng build portal-app
```
