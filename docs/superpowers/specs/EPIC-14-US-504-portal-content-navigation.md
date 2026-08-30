# Portal content navigation and collections

**Epic:** EPIC-14 Portal Content Experience  
**Story:** US-504 Portal FAQ and article discovery  
**Status:** Implemented

## Objective

Give signed-in portal users separate, usable surfaces for FAQs, support articles, and the solution
overview. Navigation must highlight only the current page, and selected navigation icons must remain
readable on the white icon tile.

## Scope

- Exact-match portal sidebar and mobile drawer navigation.
- `/app/faq` FAQ collection backed by the public knowledge-base FAQ endpoint.
- `/app/articles` article collection backed by the paged knowledge-base endpoint.
- `/app/solution` authenticated entry point to the existing solution page.
- Responsive card layouts, search, loading/error/empty states, and pagination.
- English and Arabic labels for new navigation and collection UI.

## Implemented flow

```ts
// frontend/projects/portal-app/src/app/app.routes.ts
{ path: 'faq', component: PortalContentPageComponent },
{ path: 'articles', component: PortalContentPageComponent },
{ path: 'solution', component: PortalSolutionComponent },
```

```ts
// frontend/projects/portal-app/src/app/features/kb/content-page.component.ts
const request = this.isFaq
  ? this.api.faq(term, (this.page() - 1) * this.pageSize(), this.pageSize())
  : this.api.list(term, this.page(), this.pageSize());
```

The page keeps the server's `totalCount`, requests the correct page, and renders real
`ContentSummary` records. No mock FAQ or article rows are introduced.

## Acceptance criteria

- **AC-14.1** `/app/faq`, `/app/articles`, and `/app/solution` resolve to declared routes.
- **AC-14.2** A route is active only when its complete path matches; `/app/tickets/new` does not
  also highlight `/app/tickets`.
- **AC-14.3** Active nav icons render on a white tile with the primary colour; inactive icons remain
  visible on the neutral tile.
- **AC-14.4** FAQ and article pages show API-backed cards, search, loading, error, empty, and
  paginated states.
- **AC-14.5** Collection labels are localized in English and Arabic.
- **AC-14.6** The portal build completes without Angular errors.

## Verification

`npm run build -- --project portal-app` passes. The configured initial bundle budget warning remains
and is unrelated to this feature.
