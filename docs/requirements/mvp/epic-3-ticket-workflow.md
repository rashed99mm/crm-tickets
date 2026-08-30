# Epic 3 — Ticket workflow

**Brief area:** 2 (Ticket Management) — create and track, categories and priorities, assign to
agents, status and escalation, ticket history
**Status:** **complete.** All five stories shipped and tested — `AC-29`…`AC-50`, 242 backend tests.

**Escalation is deliberately absent.** Brief area 2 lists "status and escalation"; escalation is a
rule engine that belongs with SLA (area 5), which is out of the MVP. What ships is the **status
machine**, which is what escalation would later hang from.

---

## `MVP-07` — Raise a ticket

**As an** agent, **I want** to log a customer's request with a category and priority, **so that** it
is tracked rather than remembered.

**Status:** `done` — `AC-29`, `AC-30`, `AC-31`, `AC-59`, `AC-60`.

### Acceptance criteria

1. Given a subject, customer, category and priority, a ticket is created **`New`, unassigned, with a
   human-readable reference** like `TKT-001042`.
2. Given a missing subject, an over-length field or an unknown priority, I am told which field.
3. Given a customer or category that does not exist, I am told **which one**, keyed to that field.
4. The form's own rules mirror the server's, and a server rejection lands **on the control that
   caused it** — not in a banner at the top.
5. Category is chosen from a fixed list. Free text is refused.

### Notes

Criterion 1's reference exists because "ticket 4192" is not something a person reads aloud to a
customer. It comes from a database sequence, not `MAX + 1`, which races.

Criterion 4 is why this story was built first among the vertical ones: it is the first thing in the
product that proves a field-keyed server rejection is actually bindable to a form control.

---

## `MVP-08` — Work the queue

**As an** agent, **I want** to narrow the queue to what I care about, **so that** I can find my next
piece of work in one screen.

**Status:** `done` — `AC-32`, `AC-33`, `AC-34`, `AC-57`, `AC-58`.

### Acceptance criteria

1. Given tickets exist, the queue is paged and **newest first by default**.
2. Given filters for status, priority, customer or assignee, only matching tickets appear — **and the
   filters combine**, narrowing to the intersection.
3. Given a "my tickets" toggle, I see only work assigned to me, resolved **from my session** and not
   from anything I could type in the URL.
4. Given a filter value that is not a real status, the request is **refused** — not answered with an
   empty page.
5. Loading, empty and error are three visually distinct states, and an empty filtered result says the
   filter matched nothing rather than claiming the queue is empty.

### Notes

Criterion 2's combination is the one that fails in practice: a handler that overwrites the predicate
instead of conjoining it passes every single-filter test and fails every real use.

Criterion 3 is a security criterion in disguise. If `mine=true&assigneeId=<someone else>` returned
that person's queue, the toggle would be an information-disclosure endpoint with a friendly name.

Criterion 4 matters because the alternative failure is silent: a typo'd filter returning nothing
reads as "no tickets in that state" and is indistinguishable from the truth.

---

## `MVP-09` — See a ticket in full

**As an** agent, **I want** one screen with the request, who it is for, and everything that has
happened to it, **so that** I can act without opening four things.

**Status:** `done` — `AC-35`, `AC-36`, `AC-50`, `AC-61`.

### Acceptance criteria

1. Given a ticket, I see its subject, description, status, priority, category and a **customer
   summary** — without a second request.
2. Given a ticket with history, entries read **newest first**, each naming the person who acted.
3. Given an unknown ticket, I am told it does not exist.
4. Actions I am not permitted to take are **not offered** — and are refused by the server if called
   anyway.

### Notes

Criterion 2 shows a name; the stored row holds only an id. Writing the name into the row would freeze
a value that changes and duplicate personal data into a table that, by design, can never be
corrected.

Criterion 4's second half is the actual control. Hiding a button is a courtesy.

---

## `MVP-10` — Move a ticket along its lifecycle

**As an** agent, **I want** to move a ticket only along paths that make sense, **so that** the status
means something to everyone reading it.

**Status:** `done` — `AC-37`…`AC-41`, `AC-47`.

### Acceptance criteria

1. Permitted moves: `New→Open`, `Open→Pending`, `Open→Resolved`, `Pending→Open`, `Pending→Resolved`,
   `Resolved→Closed`, `Resolved→Open`, `Closed→Open`.
2. **Every other move is refused as a conflict** — the request is well-formed, the state is wrong.
   `New→Closed` is not a validation error.
3. Moving a ticket to the status it already holds is refused.
4. Reopening a `Resolved` or `Closed` ticket returns it to `Open` and **records it as a reopen**,
   distinctly from an ordinary status change.
5. Given two people changing the same ticket at once, the second is refused and **the first change
   survives**. No silent overwrite.
6. Only the ticket's assignee may move it — or a supervisor, who may move any.

### Notes

Criterion 2's distinction from a malformed request is the one this whole epic is built around, and it
is why an unknown status like `Escalated` answers differently from an impossible move like
`New→Closed`.

Criterion 5 is why the ticket carries a version the client echoes back. Without it the column is
decoration: two sequential requests each read the current value and both succeed.

Criterion 6 cannot be enforced at the endpoint. The same person, role and verb produce success or
refusal depending on **which ticket** — knowable only after it is loaded.

---

## `MVP-11` — Hand work to the right person

**As a** supervisor, **I want** to assign and reassign tickets, **so that** work reaches whoever
should do it.

**Status:** `done` — `AC-42`…`AC-46`, `AC-48`, `AC-49`.

### Acceptance criteria

1. Given a supervisor, I can assign an unassigned ticket to an agent, and reassign an assigned one.
2. A reassignment records **who held it before**.
3. **An agent cannot assign anything** — including a ticket already assigned to themselves.
4. Given a target who does not exist, or who is not an agent, the assignment is refused and I am told
   which field is wrong.
5. Every creation, assignment, reassignment, status change and reopen appends a history row naming
   the actor, the time, and the values it moved between.
6. **History cannot be altered or deleted** by any code path or endpoint.

### Notes

Criterion 3's parenthetical is the one a reasonable-looking shortcut gets wrong: permission precedes
ownership, because assignment is a supervisory act regardless of who currently holds the ticket.

Criterion 4 needs a **role lookup, not an existence check** — a supervisor is a real user with a real
id, so checking only that the target exists would let tickets be assigned to the knowledge-base
editor.
