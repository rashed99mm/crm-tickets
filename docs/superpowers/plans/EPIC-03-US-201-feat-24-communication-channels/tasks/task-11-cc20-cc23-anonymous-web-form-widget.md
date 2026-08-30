# Task 11 — Anonymous Web-Form Widget

**Criteria:** `FB-6`, `FB-7`, `FB-8`, `FB-9` / `CC-20`..`CC-23`  
**Status:** pending  
**Commit:** none

## Context

The authenticated portal submission is `portal-app/features/tickets/submit.component.ts` and belongs
to FEAT-22. This task adds a separate public route beside the anonymous routes at
`portal-app/src/app/app.routes.ts:13-17`. Field errors follow `ApiError`/`FieldError` in
`common/src/lib/api/api-response.ts:19-34`, matching `ticket-messages.component.ts:148-157`.

## Files

- Create `common/src/lib/channels/web-form.api.ts` and its tests.
- Create `portal-app/src/app/features/web-form/web-form.component.ts/html/spec.ts`.
- Modify `portal-app/src/app/app.routes.ts`, `common/src/public-api.ts`, and translations.

## Steps

1. Confirm the implemented ExternalApi route and request property names.
2. Write HttpTestingController tests for valid submission, field errors, and indistinguishable
   honeypot/rate-limit success responses.
3. Implement name, email, subject, description, and a non-visible honeypot control.
4. Map server field errors to controls using the existing interceptor/API error pattern.
5. On success display only the ticket reference; never render internal ids.
6. Keep the route anonymous and separate from the authenticated ticket form.
7. Add English/Arabic copy and verify logical RTL layout.

## Run

```text
cd frontend
npx ng test common --watch=false --include="**/web-form.api.spec.ts"
npx ng test portal-app --watch=false --include="**/web-form.component.spec.ts"
```
