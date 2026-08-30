# FEAT-21 AI Assist — Frontend Design

**Date:** 2026-08-27
**Status:** Approved (continuation of the approved backend epic)
**Depends on:** Backend `EPIC-11-US-701-feat-21-ai-assist-design.md` (shipped)
**Apps:** admin-app (drafting assistant), portal-app (QA chat widget)

## Assumptions

- **A1 — Availability drives affordances.** The degraded answer is `503 ERR052`; the UI hides or
  disables every AI affordance after seeing it once per session rather than showing buttons that
  always fail.
- **A2 — The human gate is visual.** Every generated block renders as a *pending draft*: editable,
  with Accept / Reject controls. Nothing posts until Accept; Reject discards locally and resolves
  server-side so tracking stays truthful.
- **A3 — Chat never claims authority.** Answers render with their citations as links to KB
  articles; the refusal (`ERR053`) renders "ask a human" copy from the dictionary, not an apology
  invented client-side.
- **A4 — No hardcoded strings.** All copy through `| t` with new `ai.*` keys (en + ar), keeping the
  RTL/no-hardcoded-string guards green.

## Acceptance criteria

- **AC-F1** *(contract parity)* — `AiApi` in common calls the six internal endpoints + external
  `/knowledge-base/ask`, unwrapped by the envelope interceptor; errors arrive as typed `ApiError`.
- **AC-F2** *(US-704)* — ticket detail shows an "AI summary" action; result renders pending, accept/reject works.
- **AC-F3** *(US-706)* — "Draft with AI" fills the message composer as **editable text**; sending uses the existing record-message flow unchanged.
- **AC-F4** *(US-707)* — solutions sidebar lists cited articles linking to the detail page (route-consistent).
- **AC-F5** *(US-705)* — category suggestions render as options; accepting applies through the suggestion command.
- **AC-F6** *(US-708)* — resolved suggestions can't be re-resolved (controls disappear once Accepted/Rejected).
- **AC-F7** *(QA behaviour)* — portal chat widget sends questions, renders grounded answers with citation links, renders the ERR053 refusal distinctly, disabled-while-busy.
- **AC-F8** *(gates)* — component suites green for common/admin/portal; both builds clean; no-hardcoded-string + rtl-safety guards stay green.

## Out of scope

Streaming tokens, chat transcript persistence, cross-conversation memory, analytics dashboards on acceptance rate (backend query exists via US-708 rows).
