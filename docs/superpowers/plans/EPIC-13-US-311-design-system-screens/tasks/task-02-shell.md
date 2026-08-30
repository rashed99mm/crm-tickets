# Task 2 — The shell

| Field | Value |
|---|---|
| Plan | [`../implementation-plan.md`](../implementation-plan.md) |
| Feature | Command Center design application — shell and screens |
| Criteria | `AC-86`, `AC-92` |
| Status | `done` |
| Commit | _not committed_ |

## Files

- `frontend/projects/admin-app/src/app/layout/shell.component.ts`
- `frontend/projects/admin-app/src/app/layout/shell.component.html`
- `frontend/projects/admin-app/src/app/layout/nav-routes.spec.ts` (new — `AC-92`)
- `frontend/projects/portal-app/src/app/layout/shell.component.ts`

## What was done

`AC-86`. 280px sidebar on `surface-low` with `border-e`, a `size-10 rounded-lg bg-primary` brand
mark beside the product name, icon-and-label nav items, an indigo `bg-secondary-container` pill on
the active one with its icon `filled`, and a bottom group separated by `mt-auto pt-6 border-t`.
64px `surface-lowest` topbar with a bottom border. `main` is `flex-1 overflow-y-auto bg-surface
p-6`, so the sidebar and topbar no longer scroll with the content — and every screen dropped its
own `p-6`, which would otherwise have doubled.

Icons: `dashboard` · `confirmation_number` · `group` · `badge` · `lock`.

`AC-92`. `NAV_ITEMS` is exported and `nav-routes.spec.ts` asserts every entry resolves to a path
declared in `app.routes.ts`. None of the mockups' search box, notification bell, "Pulse AI
Assistant", Knowledge Base or Reports was added.

## Deviations from the plan

1. **A nav item is a wrapper `<div>` around its anchor, not the anchor itself.** A Material Symbol
   renders by ligature, so `<cs-icon>` inside the `<a>` puts the literal word `dashboard` into the
   link's text content — and `AC69`/`AC77` in `shell.component.spec.ts` read
   `querySelectorAll('nav a').map(a => a.textContent.trim())` and compare against `'Dashboard'` and
   `'Customers'`. Those tests are not ours to edit, and they are right to read the accessible name.
   So the icon sits beside the anchor, `routerLinkActive` moves to the wrapper (it has to colour
   the icon too), and the anchor's `::before` is stretched over the pill with `before:absolute
   before:inset-0 before:content-['']` so the icon and the padding stay inside the click target.
   Verified in the built CSS, not assumed: `.before\:absolute:before{content:var(--tw-content);
   position:absolute}` and `.before\:content-\[\'\'\]:before{--tw-content:""}` are both emitted.

2. **Sign-out stays in the topbar; the sidebar's bottom group holds the signed-in identity.** The
   plan anchors sign-out to the foot of the sidebar, but
   `AC63: the topbar heading comes from the dictionary, not a literal` asserts
   `el.querySelector('header')?.textContent` contains `TRANSLATIONS['auth.signOut'].ar`. Moving it
   would have failed a test that exists for a different and still-valid reason. Putting it in both
   places would be two controls for one action. The bottom group therefore carries the user's name
   and a `person` glyph — real data, where the mockups have a Help Center link to a page this
   product does not have (`AC-92` again).

3. **No "Enterprise Tier" line under the brand.** The mockups' sidebar carries one; this product has
   no tiers. Inventing a subtitle would be the same class of lie as the Reports nav item.

## Test evidence

`ng test admin-app --watch=false` → **118 passed (17 files)**, including the five pre-existing
`AdminShell` tests unedited and the two new navigation tests. `ng test common --watch=false` →
**100 passed**, which is what proves the RTL and hardcoded-string guards still hold over the new
markup.

## The point of this task

`AC-92`'s test is the one worth having. Everything else here is visual and would be locked in by
assertions that prove nothing; "every nav item resolves to a declared route" is the single
mechanical check that stops a designer's decorative sidebar becoming a product claim.
