import { Routes } from '@angular/router';
import { routes } from '../app.routes';
import { NAV_ITEMS } from './shell.component';

/**
 * `AC-92` — no control is added for a feature that does not exist.
 *
 * This is the assertion the Command Center design application most needed. The mockups' sidebar
 * carries a global search box, a notification bell, a "Pulse AI Assistant" button and Knowledge
 * Base / Reports entries; this product has none of them. Copying the chrome is the point of the
 * work, and copying those five is the one way it goes wrong — a nav item that goes nowhere is a
 * lie about what the product does, and it is a lie a reviewer only catches by clicking.
 *
 * Reading `NAV_ITEMS` against `app.routes.ts` turns that from a review question into a build
 * failure. It also catches the quieter version of the same bug: a route renamed in one file and
 * not the other, which leaves a real nav item pointing at a 404.
 */
function declaredPaths(children: Routes, prefix = ''): string[] {
  const found: string[] = [];

  for (const route of children) {
    // A wildcard or a bare redirect declares no destination of its own; the paths it resolves to
    // are declared elsewhere in the table and are collected there.
    if (route.path === undefined || route.path === '**') {
      continue;
    }

    const full = [prefix, route.path].filter((segment) => segment.length > 0).join('/');
    found.push(`/${full}`);

    if (route.children) {
      found.push(...declaredPaths(route.children, full));
    }
  }

  return found;
}

describe('AdminShell navigation', () => {
  it('AC92: every nav item resolves to a declared route', () => {
    const declared = declaredPaths(routes);

    // Reported as the offending list rather than one failed `toContain` at a time, so a failure
    // names every stray entry instead of sending the next person round the loop five times.
    const unreachable = NAV_ITEMS.filter((item) => !declared.includes(item.path)).map(
      (item) => item.path,
    );

    expect(unreachable).toEqual([]);
  });

  /**
   * The other direction. `AC-92` is about controls for features that do not exist; this catches
   * the inverse — a screen that exists and has no way in, which is gap `G-5` all over again (the
   * customer screens were reachable only through a `<select>` for two phases).
   *
   * `tickets/new`, the detail routes and `forbidden` are deliberately excluded: they are reached
   * from within a listed screen, not from the sidebar.
   */
  it('every top-level screen route is offered by the sidebar', () => {
    const reachedFromInsideAnotherScreen = new Set([
      '/tickets/new',
      '/tickets/:id',
      '/customers/new',
      '/customers/:id',
      '/forbidden',
      '/login',
      '/',
      // Live-chat session detail is opened from the /chat queue screen.
      '/chat/sessions/:id',
    ]);


    const listed = new Set(NAV_ITEMS.map((item) => item.path));
    const orphaned = declaredPaths(routes).filter(
      (path) => !reachedFromInsideAnotherScreen.has(path) && !listed.has(path),
    );

    expect(orphaned).toEqual([]);
  });
});
