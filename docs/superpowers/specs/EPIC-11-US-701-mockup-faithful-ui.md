# Mockup-Faithful UI: all Stitch screens

**Date:** 2026-08-27  
**Status:** Approved  
**Type:** Cross-cutting frontend epic  
**Epic:** `EPIC-13`  
**Source:** `stitch_smart_support_ticketing_crm/`

## Problem

The Angular applications contain functional screens, but their layout, density, chrome and
responsive behaviour do not consistently reproduce the supplied Stitch reference screens. A
reviewer or user comparing the running application with the supplied HTML and PNG references can
find missing screen regions, different column compositions, different palettes and desktop layouts
that have no deliberate mobile or tablet behaviour.

## Assumptions

- **A1.** The supplied `code.html` files are the authoritative source for structure, typography,
  spacing, colour and component states. `screen.png` is used for visual comparison where it contains
  a real image.
- **A2.** The mockup folder contains 16 `code.html` screen references and two design-system
  references. The implementation covers all 16 screen references listed below; `command_center`
  and `proton_precision` provide shell/palette guidance and are not additional routes.
- **A3.** Tailwind CSS v4 remains the styling engine. It is already installed, configured through
  `frontend/.postcssrc.json`, and used by `frontend/projects/common/src/styles/theme.css`.
- **A4.** The two supplied design systems remain visually distinct. Command Center screens use its
  blue primary palette; Proton Precision screens use its black primary palette. Shared semantic
  status and priority colours remain readable in both systems.
- **A5.** Existing API, routing, authentication, i18n and signal behaviour remain authoritative.
  Visual work does not silently change business behaviour or invent API contracts.
- **A6.** Designed regions without backing data still render in their designed position with an
  honest non-interactive `not recorded` or `not available` state. No fabricated customer, analytics,
  AI or avatar data is presented as real.
- **A7.** Responsive behaviour is defined at 375px, 768px, 1280px and 1920px. Intermediate widths
  must interpolate without horizontal page overflow.

## Out of scope

- Native mobile applications.
- New backend entities, endpoints, migrations or permissions solely to fill visual gaps.
- Replacing Angular standalone components, signals or the shared library architecture.
- Introducing a second CSS framework or CDN-hosted Tailwind at runtime.
- Treating screenshot similarity as proof that a control works; behaviour still requires tests.

## Screen inventory

| Screen family | Reference directory | Application surface | Palette |
|---|---|---|---|
| Admin dashboard | `admin_dashboard` | `admin-app` dashboard/admin landing | Proton |
| Admin ticket management | `admin_ticket_management` | ticket/customer management table | Proton |
| Agent dashboard | `agent_dashboard_overview` | `admin-app` dashboard | Command Center |
| AI agent workspace | `ai_powered_agent_workspace` | ticket detail workspace | Proton |
| AI ticket workspace | `ai_ticket_management_workspace` | ticket management workspace | Proton |
| Command Center shell reference | `command_center` | shared staff shell | Command Center |
| CRM landing | `command_center_crm_landing_page` | portal/public landing variant | Command Center |
| Account creation | `create_your_account` | `portal-app` signup | Proton |
| Customer 360 | `customer_360_history` | customer detail | Command Center |
| Customer profile | `customer_profile_history` | customer detail | Proton |
| Knowledge base | `knowledge_base_management` | KB management/portal surface | Proton |
| SLA analytics | `management_analytics_sla_performance` | reports | Command Center |
| Submit ticket | `submit_ticket` | ticket creation | Proton |
| Ticket detail chatbot | `ticket_detail_chatbot` | ticket detail | Proton |
| Ticket queue | `ticket_queue` | ticket queue | Proton |
| User dashboard | `user_dashboard` | `portal-app` home | Command Center |
| User profile | `user_profile_settings` | `portal-app` settings | Proton |

## Acceptance criteria

### Design system and shared chrome

- **AC-400.** Given either application renders a routed screen, when its shell is inspected, then
  the shared navigation, header, typography, spacing and surface classes come from shared Angular
  components or shared Tailwind tokens rather than duplicated screen-specific CSS.
- **AC-401.** Given a Command Center screen, when it renders, then its primary, surface, text and
  accent values resolve to the Command Center tokens from the mockup.
- **AC-402.** Given a Proton Precision screen, when it renders, then its primary, surface, text and
  accent values resolve to the Proton tokens from the mockup without changing Command Center
  screens.
- **AC-403.** Given a screen contains a status or priority, when it renders in either palette, then
  its semantic colour, contrast and label remain distinguishable and readable.
- **AC-404.** Given the user changes locale to Arabic, when any adapted screen renders, then layout,
  navigation, icons, text alignment and spacing mirror correctly without physical-direction utility
  classes.

### Screen composition

- **AC-405.** Given the staff shell loads, when the viewport is desktop width, then the sidebar rail,
  top header, active navigation treatment and page canvas match the `command_center` composition.
- **AC-406.** Given the dashboard loads, when data is available, then its bento/stat region, active
  ticket region, activity region and chart/summary regions match `agent_dashboard_overview`.
- **AC-407.** Given the queue loads, when tickets are available, then filters, table header, ticket
  rows, semantic pills, pagination and empty/loading/error states match `ticket_queue`.
- **AC-408.** Given ticket creation loads, when the form is displayed, then its heading, field order,
  priority control, description area, attachment zone and action footer match `submit_ticket`.
- **AC-409.** Given ticket detail loads, when a ticket is selected, then its identity header,
  conversation/timeline, metadata rail, chatbot/AI region and action controls match the applicable
  `ticket_detail_chatbot`, `ai_powered_agent_workspace` and `ai_ticket_management_workspace` layouts.
- **AC-410.** Given a customer profile loads, when customer data is available, then its identity
  band, contact/account rail, activity centre and files/actions rail match `customer_profile_history`
  and `customer_360_history`.
- **AC-411.** Given administration screens load, when their tables or forms are displayed, then
  `admin_dashboard`, `admin_ticket_management`, `knowledge_base_management` and
  `management_analytics_sla_performance` preserve their reference hierarchy, density and controls.
- **AC-412.** Given the portal loads, when a customer visits landing, signup, dashboard or profile,
  then the relevant screen matches `command_center_crm_landing_page`, `create_your_account`,
  `user_dashboard` and `user_profile_settings`.

### Responsive and state behaviour

- **AC-413.** Given any adapted screen is viewed at 375px, when it renders, then content remains
  usable without horizontal page scrolling, the sidebar becomes an accessible drawer, and each
  multi-column composition follows its documented mobile stacking order.
- **AC-414.** Given any adapted screen is viewed at 768px, when it renders, then tablet spacing,
  navigation, tables, cards and forms use the documented tablet composition without clipped content.
- **AC-415.** Given any adapted screen is viewed at 1280px or 1920px, when it renders, then the
  intended desktop columns, max widths, gutters and density match the reference composition.
- **AC-416.** Given a screen's API request is loading, empty or fails, when the state renders, then
  loading, empty and error states are visually consistent with the adapted screen and remain
  distinguishable; errors expose retry where the existing feature supports retry.
- **AC-417.** Given a designed region has no backing API data, when it renders, then its position and
  visual weight remain present but its content is explicitly labelled as unavailable and is not an
  enabled action.
- **AC-418.** Given a user navigates, opens a drawer/dialog, submits a form or encounters an error,
  when keyboard navigation is used, then focus order, labels, landmarks, button names and focus
  restoration remain accessible.

### Verification and regression

- **AC-419.** Given all adapted templates are checked, when the RTL safety and no-hardcoded-strings
  tests run, then they pass with no physical-direction utilities or untranslated visible strings.
- **AC-420.** Given the frontend builds, when `npx ng build admin-app` and the portal build are run,
  then both complete without warnings-as-errors or missing style assets.
- **AC-421.** Given the visual verification suite runs, when it captures every inventory screen at
  375px, 768px, 1280px and 1920px, then each capture is reviewed against its `code.html`/`screen.png`
  reference and deviations are recorded rather than silently accepted.
- **AC-422.** Given the visual adaptation is complete, when all existing frontend tests run, then
  pre-existing functional, routing, i18n, API and state tests remain green.

## Design

### Styling approach

Keep Tailwind v4. Port the reference utility structure into Angular templates, but replace CDN
configuration and inline page-level config with shared CSS tokens. Use logical utilities only:
`ps-*`, `pe-*`, `ms-*`, `me-*`, `start-*`, `end-*`, `text-start` and `text-end`.

Add a palette attribute to the rendered application shell, for example `data-design-system`, and
scope Proton token overrides there. Components consume semantic utilities and do not branch on the
palette in TypeScript. The existing `theme.css` remains the token source of truth after its token
groups are expanded.

### Shared components

Build or extend shared presentational components only where at least two screens share the pattern:
app shell, responsive sidebar, top header, page heading, stat card, data table, status/priority pill,
timeline, metadata rail, attachment list, empty/loading/error state, drawer, dialog, search and
notification surfaces. Feature components own screen composition and data fetching.

### Testing

Component tests name the relevant `AC-400`–`AC-422` criterion and verify rendered landmarks,
palette attributes, responsive class contracts, state branches, accessible names and interactions.
Playwright is reserved for the final visual matrix and the existing terminal journey; screenshot
comparisons are reviewed with a written deviation log. No test passes solely because a screenshot
exists.
