# Task 1 — Fonts

| Field | Value |
|---|---|
| Plan | [`../implementation-plan.md`](../implementation-plan.md) |
| Feature | Command Center design application — shell and screens |
| Criteria | supports `AC-86`…`AC-89` (assumption `A21`) |
| Status | `done` |
| Commit | _not committed — the plan says do not commit_ |

## Files

- `frontend/projects/admin-app/src/index.html`
- `frontend/projects/portal-app/src/index.html`

## What was done

Both documents now request Material Symbols Outlined, preceded by `preconnect` to
`fonts.googleapis.com` and `fonts.gstatic.com`.

## Deviations from the plan

**The plan says "add Material Symbols beside the existing Google Fonts link". There was no
existing Google Fonts link.** `theme.css` has named Hanken Grotesk, Inter and JetBrains Mono since
the Phase 2 token extraction, and neither `index.html` ever requested any of them — every screen
built since has been rendering in the `ui-sans-serif` fallback while the theme claimed otherwise.
So this task loads four families, not one:

```
Hanken+Grotesk:wght@400..800   font-display
Inter:wght@400..700            font-sans
JetBrains+Mono:wght@400..600   font-mono, and the new text-data-mono role
Material+Symbols+Outlined      cs-icon
```

This is the only change in the whole restyle that alters how existing, untouched screens look. It
is the correct fix rather than a scope creep: the alternative is a design system whose type ramp is
decorative.

## Test evidence

Not directly testable — a font request is a network fact, not a DOM one. Covered indirectly:
`rtl-safety.spec.ts` scans `index.html` (it is not in that guard's skip list) and stays green, and
both apps build.

## The point of this task

Without the icon font, `cs-icon` renders its ligature name as literal text — the sidebar would read
"dashboard Dashboard". The icons are not decoration here; they are the thing that makes the nav and
the cards recognisable as the mockups' design.
