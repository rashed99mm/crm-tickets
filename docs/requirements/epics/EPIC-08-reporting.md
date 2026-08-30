# EPIC-08 · Reports & management

| | |
|---|---|
| **Epic** | `EPIC-08` |
| **Priority** | P1 |
| **Stories** | 0 specified — backlog only |
| **Sprints** | 13 (slice S6) |

## Goal

Provide operational visibility into support activity and performance *(rule specification §8)*.

## Status: not specified

Slice S6 has no spec. The measurement framework, KPI catalogue with formulas (`KPI-1`–`KPI-16`),
report and dashboard inventories, and the dimensional model already exist in the BRD §12 — the
future spec starts from there rather than from a blank page. Constraints it inherits: reporting
load must not degrade operational response times (`NFR-22`); the operational store serves reports
until the volume threshold in BRD §12.8 is crossed (`PA-8`, `PA-10`); dimension history is captured
from the slice that introduces each dimension because overwritten history cannot be recovered
(`RSK-7`). Last of the data-consuming sprints — every upstream source exists by sprint 13.

## Reserved backlog (rule-file titles — unspecified by design)

US-066 View Ticket Volume Report · US-067 View Ticket Status Report · US-068 View SLA Performance
Report · US-069 View Agent Performance Report · US-070 View Customer Satisfaction Report · US-071
View Management Dashboard · US-072 Filter Reports by Date · US-073 Export Report
