# Task 01: Shared UX Foundation

**Status:** Implemented in first pass  
**Criteria:** `AC-500`, `AC-501`, `AC-502`, `AC-503`  
**Scope:** `common` design tokens and reusable UI primitives.

## Files To Read First

- `docs/superpowers/specs/EPIC-01-US-101-frontend-crm-ux-refactor-design.md`
- `frontend/projects/common/src/styles/theme.css`
- `frontend/projects/common/src/lib/ui/button.component.ts`
- `frontend/projects/common/src/lib/ui/button.component.html`
- `frontend/projects/common/src/lib/ui/badge.component.ts`
- `frontend/projects/common/src/lib/ui/status-pill.component.ts`
- `frontend/projects/common/src/lib/ui/card.component.ts`
- `frontend/projects/common/src/lib/ui/input-field.component.ts`
- `frontend/projects/common/src/lib/i18n/translations.ts`
- `frontend/projects/common/src/public-api.ts`

## Intent

Create a consistent shared foundation for CRM UI: visible buttons, semantic status/priority/SLA
badges, channel indicators, forms, cards, dialogs, loading/empty/error states, chart shells, and
action bars.

## Required Changes

- Extend `CsButton` to support the full interaction set: `primary`, `secondary`, `ghost`, `danger`,
  `icon`, disabled, and busy/loading where the current API allows it.
- Ensure icon-only buttons have accessible names and stable square dimensions.
- Consolidate status, priority, SLA, escalation, and channel visual treatment into shared primitives
  or literal class maps. Do not build Tailwind class names dynamically.
- Add or refine a chart container primitive if reports need repeated loading/empty/error/fallback
  behavior.
- Confirm shared states use clear icons, concise localized text, retry only where supported, and no
  layout shift.
- Add any missing translation keys in English and Arabic.

## Implementation Notes

- Prefer extending existing `common/src/lib/ui` components over adding screen-specific styling.
- Keep presentational components free of HTTP and feature state.
- Use logical utilities only: `ps`, `pe`, `ms`, `me`, `start`, `end`, `text-start`, `text-end`,
  `border-s`, `border-e`.
- Preserve existing behavior covered by specs, especially form validation and retry semantics.

## Code Context And Examples

Current button contract:

```ts
// frontend/projects/common/src/lib/ui/button.component.ts
const VARIANTS = {
  primary: 'bg-primary text-on-primary shadow-sm hover:opacity-90 active:scale-95',
  secondary:
    'bg-surface-lowest border border-outline-variant text-on-surface hover:bg-surface-bright',
  ghost: 'text-primary hover:underline',
} as const;
```

Additive target shape:

```ts
const VARIANTS = {
  primary: 'bg-primary text-on-primary shadow-sm hover:opacity-90 active:scale-95',
  secondary:
    'border border-outline-variant bg-surface-lowest text-on-surface hover:bg-surface-bright',
  ghost: 'text-primary hover:bg-surface-highest',
  danger: 'bg-error text-on-error shadow-sm hover:opacity-90 active:scale-95',
  icon: 'grid size-9 place-items-center rounded-lg text-on-surface-variant hover:bg-surface-highest',
} as const;

export class CsButton {
  readonly variant = input<keyof typeof VARIANTS>('primary');
  readonly type = input<'button' | 'submit'>('button');
  readonly disabled = input(false);
  readonly busy = input(false);
  readonly ariaLabel = input<string>();
  readonly pressed = output<void>();
}
```

Example status/SLA extension:

```ts
type IndicatorKind = 'status' | 'priority' | 'sla' | 'channel' | 'escalation';

const SLA_TONE: Readonly<Record<string, string>> = {
  healthy: 'border-success/20 bg-success/10 text-success',
  warning: 'border-warning/20 bg-warning/10 text-warning',
  breached: 'border-error/20 bg-error/10 text-error',
  paused: 'border-outline-variant bg-surface-highest text-on-surface-variant',
};
```

Example task-facing usage:

```html
<cs-button variant="primary" [busy]="saving()" type="submit">
  {{ 'common.save' | t }}
</cs-button>

<cs-button variant="danger" [disabled]="!canDelete()" (pressed)="confirmDelete()">
  <cs-icon name="delete" />
  {{ 'common.delete' | t }}
</cs-button>
```

Example chart shell target:

```html
<cs-chart-frame
  [title]="'reports.ticketVolume.title' | t"
  [loading]="state().loading"
  [empty]="state().data?.items.length === 0"
  [error]="state().error?.message ?? null"
>
  <app-ticket-volume-chart [series]="state().data!.series" />
</cs-chart-frame>
```

## Suggested Tests

- `AC500_ButtonVariantsAreVisibleAndAccessible`
- `AC501_StatusPrioritySlaAndChannelIndicatorsUseSemanticClasses`
- `AC502_SharedPrimitivesUseThemeTokens`
- `AC503_PendingFailedAndDisabledActionsDoNotShiftLayout`

## Verification

Run from `frontend/`:

```text
npx ng test common --watch=false
npx ng build admin-app
npx ng build portal-app
```

## Execution Record

| Item | Result |
|---|---|
| Tests added | `button.component.spec.ts`, `channel-pill.component.spec.ts`, `sla-pill.component.spec.ts`, and `pagination.component.spec.ts` cover `AC-500`/`AC-501` button, channel, SLA, and pagination control visibility. |
| Commands run | `npx ng test common --watch=false` passed: 44 files, 195 tests. First sandboxed run failed with `spawn EPERM`; rerun with approval succeeded. |
| Deviations | `cs-chart-frame` was added as a reusable shell but not yet adopted by report pages in this pass. Inline templates in shared UI additions were split into separate `.html` files to satisfy the TS/HTML separation rule. |
| Commit | Pending |
