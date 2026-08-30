# EPIC-14-US-504 implementation plan

## 1. Navigation contract

Update the portal shell's single `NAV_ITEMS` table and use exact matching in both desktop and mobile
templates:

```ts
const NAV_ITEMS = [
  { path: '/app/faq', key: 'portal.nav.faq', icon: 'quiz' },
  { path: '/app/articles', key: 'portal.nav.articles', icon: 'article' },
  { path: '/app/solution', key: 'portal.nav.solution', icon: 'auto_awesome' },
];
```

```html
[routerLinkActiveOptions]="{ exact: true }"
```

This prevents parent routes from receiving an active style when a child page is open.

## 2. Collection API flow

Reuse `ContentsApi`, which already owns the backend contract:

```ts
this.api.faq(term, (this.page() - 1) * this.pageSize(), this.pageSize());
this.api.list(term, this.page(), this.pageSize());
```

The component converts server responses into `AsyncState<PagedResult<ContentSummary>>`, preserving
the existing loading/error conventions used by the portal.

## 3. UI composition

`PortalContentPageComponent` renders a shared page header, search control, responsive cards, and
pagination. The card link opens the existing detail route, so content remains connected to the
current article body and helpfulness flow:

```html
<a [routerLink]="['/app/kb', item.id]" class="group flex min-h-56 ...">
  <h2>{{ item.title }}</h2>
</a>
```

## 4. Localization

Add translation keys to `frontend/projects/common/src/lib/i18n/translations.ts` for navigation,
page titles, descriptions, fallback category text, and the read action. Templates pass keys through
`TranslatePipe`; user-facing copy is not hardcoded in the component.

## 5. Verification checklist

- Build `portal-app`.
- Open `/app`, `/app/tickets`, `/app/tickets/new`, `/app/faq`, `/app/articles`, and `/app/solution`
  and confirm exactly one selected nav item.
- Search FAQ and articles and confirm Enter and button submission reset page one.
- Move between pages and confirm previous/next controls use the API `totalCount`.
- Toggle Arabic and confirm labels and logical layout remain usable.
