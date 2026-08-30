import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Title } from '@angular/platform-browser';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import {
  BrandingStore,
  CsButton,
  CsConfirmationHost,
  CsIcon,
  CsLanguageSwitcher,
  CsToastHost,
  LocaleStore,
  NotificationApi,
  NotificationStore,
  notificationFromDto,
  RealtimeService,
  SessionStore,
  TranslatePipe,
  TranslationKey,
} from 'common';
import { filter, map } from 'rxjs';

/** One sidebar entry: where it goes, what it is called, and its Material Symbols ligature. */
export interface NavItem {
  readonly path: string;
  readonly key: TranslationKey;
  /** Material Symbols Outlined ligature. Not translatable — it is a glyph name. */
  readonly icon: string;
  readonly category: NavCategory;
  readonly adminOnly?: true;
  /** Visible to Admin or Supervisor — distinct from `adminOnly`, which means Admin alone. */
  readonly supervisorOrAdmin?: true;
  /** Resolves for the tab title but is not rendered as a sidebar link — `/profile` is reached
   * from the identity footer, not the nav list, but still needs a real name in the browser tab. */
  readonly hidden?: true;
}

type NavCategory = 'workspace' | 'operations' | 'intelligence' | 'administration';

interface NavSection {
  readonly category: NavCategory;
  readonly key: TranslationKey;
  readonly items: readonly NavItem[];
}

/**
 * The sidebar, and the source of the topbar heading.
 *
 * One table, two readers: `nav()` filters it for the sidebar, `title()` matches the active url
 * against it. Two tables would drift, and the drift would show as a topbar naming the wrong screen
 * — which is exactly the bug this replaced.
 *
 * Staff management is only *listed* for administrators (AUTH-22, using the backend's real
 * Admin/User role vocabulary per FE-2/FE-14). The route guard and the endpoint policy are what
 * actually refuse a non-admin; this only avoids showing a link that would bounce. It stays in the
 * table regardless, so the heading is right even on a route the sidebar is not offering.
 *
 * **Exported so `AC-92` can be tested rather than reviewed.** The Command Center mockups put a
 * global search box, a notification bell, a "Pulse AI Assistant" button and Knowledge Base /
 * Reports entries in this sidebar. This product has none of them, and a nav item that goes nowhere
 * is a lie about what the product does — so `shell.component.spec.ts` asserts every entry here
 * resolves to a route declared in `app.routes.ts`.
 */
export const NAV_ITEMS: readonly NavItem[] = [
  { path: '/dashboard', key: 'nav.dashboard', icon: 'dashboard', category: 'workspace' },
  { path: '/agent-workspace', key: 'nav.agentWorkspace', icon: 'support_agent', category: 'workspace' },
  { path: '/tickets', key: 'nav.tickets', icon: 'confirmation_number', category: 'workspace' },
  { path: '/customers', key: 'nav.customers', icon: 'group', category: 'workspace' },
  { path: '/chat', key: 'nav.chat', icon: 'chat', category: 'workspace' },
  { path: '/kb-admin', key: 'nav.kbAdmin', icon: 'menu_book', category: 'operations', adminOnly: true },
  { path: '/departments', key: 'nav.departments', icon: 'apartment', category: 'operations', adminOnly: true },
  { path: '/sla-policies', key: 'nav.slaPolicies', icon: 'schedule', category: 'operations', adminOnly: true },
  // "Reports" is the management overview (the management_analytics mockup); the five individual
  // report screens underneath it are each listed too, so no screen exists that a sidebar cannot reach.
  { path: '/reports/overview', key: 'nav.reportsOverview', icon: 'query_stats', category: 'intelligence', supervisorOrAdmin: true },
  { path: '/reports/ticket-volume', key: 'nav.ticketVolume', icon: 'bar_chart', category: 'intelligence', supervisorOrAdmin: true },
  { path: '/reports/sla-performance', key: 'nav.slaPerformance', icon: 'timer', category: 'intelligence', supervisorOrAdmin: true },
  { path: '/reports/agent-performance', key: 'nav.agentPerformance', icon: 'engineering', category: 'intelligence', supervisorOrAdmin: true },
  { path: '/reports/csat', key: 'reports.csat.title', icon: 'sentiment_satisfied', category: 'intelligence', supervisorOrAdmin: true },
  { path: '/reports/live-queue', key: 'nav.liveQueue', icon: 'pending_actions', category: 'intelligence', supervisorOrAdmin: true },
  { path: '/users', key: 'nav.staff', icon: 'badge', category: 'administration', adminOnly: true },
  { path: '/permissions', key: 'nav.permissions', icon: 'key', category: 'administration', adminOnly: true },
  { path: '/audit-log', key: 'nav.auditLog', icon: 'history', category: 'administration', adminOnly: true },
  { path: '/settings', key: 'nav.settings', icon: 'settings', category: 'administration', adminOnly: true },
  { path: '/profile', key: 'nav.profile', icon: 'person', category: 'administration', hidden: true },
];

const NAV_SECTION_ORDER: readonly Omit<NavSection, 'items'>[] = [
  { category: 'workspace', key: 'sidebar.category.workspace' },
  { category: 'operations', key: 'sidebar.category.operations' },
  { category: 'intelligence', key: 'sidebar.category.intelligence' },
  { category: 'administration', key: 'sidebar.category.administration' },
];

/**
 * The chrome every admin feature renders inside — the Command Center shell (`AC-86`).
 *
 * 280px sidebar on `surface-low` with a branded mark, icon-and-label nav items and an indigo
 * `secondary-container` pill on the active one; a 64px `surface-lowest` topbar with a bottom
 * border. Built with logical properties only (`border-e`, `ps-`/`pe-`, `start-`/`end-`), so
 * switching to Arabic relocates the sidebar to the right and mirrors the text with no second
 * stylesheet and no direction-specific code.
 *
 * **The nav item is a wrapper around its anchor rather than the anchor itself**, and that is
 * load-bearing rather than incidental. A Material Symbol renders by ligature, so an icon inside
 * the `<a>` puts the literal word `dashboard` into the link's text content — which is what the
 * sidebar's own tests read. The icon therefore sits beside the anchor, and the anchor's `::before`
 * is stretched over the whole pill so the icon and the padding stay part of the click target.
 * `routerLinkActive` moves to the wrapper for the same reason: it has to colour the icon too.
 *
 * **Sign-out stays in the topbar.** The mockups anchor it to the foot of the sidebar, but
 * `AC63: the topbar heading comes from the dictionary` asserts the header carries it, and that
 * test is not ours to edit. The sidebar's bottom group holds the signed-in identity instead —
 * real data, rather than the mockups' Help Center link to a page that does not exist.
 */
@Component({
  selector: 'admin-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    CsLanguageSwitcher,
    CsButton,
    CsConfirmationHost,
    CsIcon,
    CsToastHost,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.component.html',
})
export class AdminShell {
  private readonly router = inject(Router);
  private readonly documentTitle = inject(Title);
  private readonly locale = inject(LocaleStore);

  protected readonly session = inject(SessionStore);
  protected readonly notifications = inject(NotificationStore);
  private readonly notificationApi = inject(NotificationApi);
  // Instantiating RealtimeService runs its auth-reactive connection effect.
  private readonly realtime = inject(RealtimeService);
  // BrandingStore loads and applies CSS variables on startup.
  private readonly branding = inject(BrandingStore);

  /**
   * The current url, as a signal.
   *
   * `router.url` alone is not reactive, so a topbar computed from it would render once and then
   * describe whatever screen happened to be first. The initial value covers the gap before the
   * first NavigationEnd — without it the heading is blank on the very first paint.
   */
  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  /** Public so the shell's spec can exercise it directly. */
  signOut(): void {
    this.session.signOut();
    void this.realtime.stop();
    void this.router.navigateByUrl('/login');
  }

  protected readonly nav = computed(() =>
    NAV_ITEMS.filter(
      (item) =>
        !item.hidden &&
        (!item.adminOnly || this.session.hasRole('Admin')) &&
        (!item.supervisorOrAdmin || this.session.hasRole('Supervisor') || this.session.hasRole('Admin')),
    ),
  );

  protected readonly navSections = computed<readonly NavSection[]>(() => {
    const visible = this.nav();
    return NAV_SECTION_ORDER.map((section) => ({
      ...section,
      items: visible.filter((item) => item.category === section.category),
    })).filter((section) => section.items.length > 0);
  });

  /**
   * Whether the sidebar shows icon-only. Persisted per browser, not per session — a viewer's
   * screen-space preference has nothing to do with who is signed in.
   */
  protected readonly collapsed = signal(this.readStoredCollapsed());
  private readonly collapsedSections = signal<Partial<Record<NavCategory, boolean>>>({});

  isSectionCollapsed(category: NavCategory): boolean {
    return this.collapsedSections()[category] === true;
  }

  toggleSection(category: NavCategory): void {
    this.collapsedSections.update((sections) => ({
      ...sections,
      [category]: !this.isSectionCollapsed(category),
    }));
  }

  toggleCollapsed(): void {
    const next = !this.collapsed();
    this.collapsed.set(next);
    try {
      localStorage.setItem('admin-shell:sidebar-collapsed', String(next));
    } catch {
      // A private window or blocked storage loses the preference across reloads, not the toggle
      // itself — the in-memory signal above already flipped.
    }
  }

  private readStoredCollapsed(): boolean {
    try {
      return localStorage.getItem('admin-shell:sidebar-collapsed') === 'true';
    } catch {
      return false;
    }
  }

  /**
   * The active screen's name, derived from the route rather than fixed.
   *
   * This drove a topbar `<h1>` until the Command Center design was applied. The mockups' topbar
   * carries actions only, and every routed screen already renders its own heading, so the topbar
   * copy duplicated it on every page and gave each one two top-level headings. Rather than delete
   * a working route→name mapping, it now names the browser tab, which is where a per-route title
   * belongs and where nothing was setting one.
   *
   * Longest path first, so /account/password is not swallowed by a shorter prefix if one is ever
   * added above it. Falls back to the product name on a route the table does not know — /login and
   * /forbidden render outside this shell, but a future one should still get a sensible name.
   */
  protected readonly title = computed(() => {
    const url = this.url();
    const match = [...NAV_ITEMS]
      .sort((a, b) => b.path.length - a.path.length)
      .find(
        (item) =>
          url === item.path || url.startsWith(`${item.path}/`) || url.startsWith(`${item.path}?`),
      );

    return this.locale.t(match ? match.key : 'app.name');
  });

  constructor() {
    // Hydrate the inbox so a reload is not empty (FN-4).
    if (this.session.isAuthenticated()) {
      this.notificationApi
        .list(1, 50)
        .subscribe((page) => this.notifications.setAll(page.items.map(notificationFromDto)));
    }

    // Reads both the route and the locale through `title()`, so the tab follows a language
    // switch without a refetch, exactly as the rest of the shell does.
    effect(() => {
      this.documentTitle.setTitle(`${this.title()} — ${this.locale.t('app.name')}`);
    });
  }

  /** Marks a notification read via the API and reflects it in the store. */
  markRead(id: string): void {
    this.notificationApi.markRead(id).subscribe(() => this.notifications.markRead(id));
  }

  protected readonly notificationsOpen = signal(false);
  protected readonly mobileMenuOpen = signal(false);

  toggleNotifications(): void {
    this.notificationsOpen.update((v) => !v);
  }

  toggleMobileMenu(): void {
    this.mobileMenuOpen.update((open) => !open);
  }

  closeMobileMenu(): void {
    this.mobileMenuOpen.set(false);
  }
}
