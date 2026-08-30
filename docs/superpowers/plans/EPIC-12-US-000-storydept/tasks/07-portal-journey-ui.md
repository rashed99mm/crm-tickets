# Task 07 — Portal journey UI wiring (US-404–415)

## Traceability
Epic:   docs/requirements/epics/EPIC-07-customer-portal.md
Stories: US-404-portal-submit-ticket.md, US-405-portal-my-tickets.md, US-406-portal-ticket-detail.md,
         US-407-portal-reply.md, US-410-portal-login-screen.md, US-411-portal-submit-form.md,
         US-412-portal-my-tickets-list.md, US-413-portal-ticket-detail-ui.md, US-414-portal-reply-form.md,
         US-415-portal-survey-form.md
FEAT:   FEAT-22 — delivery-plan.md row 10
Spec:   docs/superpowers/specs/EPIC-07-US-404-portal-home-and-signup-design.md

## Work
Submit/list/detail/reply components already exist (portal-app/src/app/features/tickets/).
Only NEW screen: survey/feedback form (US-415) posting to the survey endpoint fixed in task 01
(US-409). Wire all flows against the unbroken backend; reply uses listMessages + record message.

## Tests
Portal flow component tests mirroring existing portal specs; US-415 survey validation
(required rating) + happy-path POST assert.

## Gate
npx ng test portal-app --watch=false → all green, output pasted.
