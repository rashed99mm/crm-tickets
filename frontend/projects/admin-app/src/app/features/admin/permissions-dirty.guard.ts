import { CanDeactivateFn } from '@angular/router';
import type { UnsavedChangesHost } from './permissions.component';

export type { UnsavedChangesHost };

/**
 * AC-806.19 — leaving the permission workbench with staged changes asks first.
 *
 * The screen owns the question (it knows how many changes and in whose language); the guard only
 * decides whether to ask. This is the codebase's first `CanDeactivateFn` — `common`'s guards
 * (`auth/guards.ts`) are all `CanActivateFn` — and it deliberately lives beside the one screen that
 * needs it rather than in the shared library. The `UnsavedChangesHost` interface is imported as a
 * type-only import so the guard does not pull the component's lazy chunk into the routes bundle
 * (`app.routes.ts` loads it via `loadComponent`).
 */
export const permissionsDirtyGuard: CanDeactivateFn<UnsavedChangesHost> = (component) =>
  component.hasUnsavedChanges() ? component.confirmLeave() : true;
