# Epic 1 — Staff access

**Brief area:** 10 (Security & Administration) — users, roles, permissions
**Why it is first:** nothing else in the product can be used, or tested, by a named person until
someone can sign in as one.
**MVP scope:** staff only. Customers do not have accounts — that is the portal, and the portal is out.

---

## `MVP-01` — Sign in and get to work

**As a** support agent, **I want** to sign in and land on my work, **so that** I can start without
hunting for it.

**Status:** `done` — `AC-1`…`AC-6`, `AC-55`, `AC-56`. Backend + Angular, tested.

### Acceptance criteria

1. Given valid credentials, when I sign in, then I reach the ticket queue holding a credential that
   carries my role.
2. Given wrong credentials, then I see a visible error, **no navigation happens**, and the message
   does not reveal whether the account exists.
3. Given repeated failures past the threshold, the account locks — and answers **identically** to a
   wrong password, because a different answer would confirm the account exists.
4. Given no session, when I open any protected screen directly, then I am sent to sign-in and
   returned to where I was headed afterwards.
5. Given I sign out, the session is gone and the back button does not restore it.

### Notes

Criterion 3 is the one that gets built wrong. A distinct "account locked" message is friendlier and
tells an attacker which addresses are real.

---

## `MVP-02` — Administer staff accounts and roles

**As an** administrator, **I want** to create staff accounts and set what each person may do,
**so that** the right people can work tickets and only supervisors can hand work out.

**Status:** `done` — acceptance pass run 2026-08-26, screen-level gap closed 2026-08-26. All five
criteria proven. Eight tests in `StaffAdministrationTests` plus two routing tests in
`app.routes.spec.ts` now name these criteria.

### The pass found a real security defect, and it was fixed

**Criterion 4 held only cosmetically.** `GetUsersInRoleAsync` filters `IsActive`, so the assign
**picker** correctly stopped offering a deactivated agent — but `AssignTicketCommandHandler` never
checked `IsActive` at all. It verified the target existed and held `Agent`, then assigned.

So the dropdown hid them and **the mutation accepted them**. A supervisor holding a page rendered
before the deactivation, or anything calling the API directly, could hand work to an account that
cannot sign in to do it — a ticket nobody would ever work. `GetAssignableAgentsQuery`'s own doc
comment claimed the filter was the enforcement; it was presentation.

The test was written first and failed against the inherited code:

```
Expected assign.StatusCode to be HttpStatusCode.BadRequest {value: 400} because work cannot be
handed to an account that can no longer sign in to do it, but found HttpStatusCode.OK {value: 200}.
```

Fixed in the handler with a field-keyed 400 and a distinct code `TICKET_ASSIGNEE_DEACTIVATED`. The
test was **not** adjusted afterwards. Proven by
`MVP02_DeactivatedAgent_CannotBeHandedWorkDirectly`.

### Criterion 2 — the suspected defect — holds

`LoginCommandHandler` **does** check `IsActive` explicitly, before the password check, answering
`ACCOUNT_DEACTIVATED`. The refresh-token leg was tested too, unprompted and correctly: refusing the
password while honouring an already-issued refresh token would have made the login check pointless.

### Criterion 3, both halves now proven

The **backend** half is proven across five routes and four verbs, signed in as a `Supervisor`. The
**screen** half — that a non-admin is not offered the staff screen and the route guard redirects to
`/forbidden` — is now proven too: `app.routes.spec.ts` carries
`MVP02: a non-admin visiting /users is sent to /forbidden, not the staff screen` and its positive
counterpart for an admin session. It lives in `app.routes.spec.ts` rather than
`users.component.spec.ts` as originally planned — `roleGuard` runs before the component is created,
so only a test with a real `Router` can observe the redirect; a component spec never gets far enough
to see it.

### Acceptance criteria

1. Given I am an administrator, I can create a staff account with a role of `Agent` or `Supervisor`.
2. Given a staff member leaves, I can deactivate them; they can no longer sign in, and **their
   history stays intact**.
3. Given I am not an administrator, the staff screen is not offered and the endpoints refuse me.
4. Given a deactivated agent, they are no longer offered as an assignee.
5. No screen, response or log ever shows a password or its hash.

### Notes

Criterion 2 is why staff are deactivated rather than deleted: a deleted agent takes the authorship of
every note and history row with them.

Criterion 4 already holds — `GetUsersInRoleAsync` filters on `IsActive` — but nothing tests it.

**The work here is verification, not construction.** If the acceptance pass finds gaps, they become
tasks; if it does not, the story closes with tests naming these criteria.
