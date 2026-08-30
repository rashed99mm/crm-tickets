# EPIC-05 · SLA & automation

| | |
|---|---|
| **Epic** | `EPIC-05` |
| **Priority** | P1 |
| **Stories** | 0 specified — backlog only |
| **Sprints** | 8 (slice S2) |

## Goal

Automatically manage response and resolution targets and support escalation workflows *(rule
specification §8)*.

## Status: not specified

Slice S2 has no spec yet, so nothing here carries criteria or allocated story ids. What exists is
the rule set the future spec must honour and the questions that block it:

- **Rules already fixed:** `BR-16` the SLA clock pauses while waiting on the customer; `BR-17` a
  target is fixed at the moment it is set — later priority changes never rewrite closed months;
  `BR-18` a reopened ticket starts a new resolution period, retaining the original.
- **Blocking:** `OQ-2` actual targets per priority, `OQ-3` business-hours vs 24/7 (`PA-1`),
  `DEP-2` branch calendars, `DEP-3` agreed targets, `OQ-12` assignment algorithm. `G-3` is
  already resolved *in sequencing*: the message record arrives at sprint 6 so attainment is
  measurable when S2 lands.

## Reserved backlog (rule-file titles — unspecified by design)

US-044 Configure SLA Policy · US-045 Calculate First Response Deadline · US-046 Calculate Resolution
Deadline · US-047 Monitor SLA Status · US-048 Notify Before SLA Breach · US-049 Detect SLA Breach ·
US-050 Escalate Breached Ticket · US-051 Automatically Assign Ticket · US-052 Execute Automation Rule
