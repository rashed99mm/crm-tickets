import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import {
  BrandingStore,
  CsIcon,
  CsLanguageSwitcher,
  AiAssistantComponent,
  LocaleStore,
  NotificationApi,
  NotificationStore,
  notificationFromDto,
  RealtimeService,
  SessionStore,
  TranslationKey,
  TranslatePipe,
} from 'common';
import { catchError, of } from 'rxjs';

interface PortalNavItem {
  readonly path: string;
  readonly key: TranslationKey;
  readonly icon: string;
}

/** The portal's primary navigation, matching the mockups' sidebar. */
const NAV_ITEMS: readonly PortalNavItem[] = [
  { path: '/app', key: 'portal.nav.dashboard', icon: 'space_dashboard' },
  { path: '/app/tickets/new', key: 'portal.nav.submit', icon: 'add_circle' },
  { path: '/app/tickets', key: 'portal.nav.tickets', icon: 'confirmation_number' },
  { path: '/app/faq', key: 'portal.nav.faq', icon: 'quiz' },
  { path: '/app/articles', key: 'portal.nav.articles', icon: 'article' },
  { path: '/app/solution', key: 'portal.nav.solution', icon: 'auto_awesome' },
  { path: '/app/feedback', key: 'portal.nav.feedback', icon: 'reviews' },
];

@Component({
  selector: 'portal-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    CsIcon,
    CsLanguageSwitcher,
    AiAssistantComponent,
    TranslatePipe,
  ],
  templateUrl: './shell.component.html',
})
export class PortalShell {
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  private readonly locale = inject(LocaleStore);
  protected readonly notifications = inject(NotificationStore);
  private readonly notificationApi = inject(NotificationApi);
  private readonly realtime = inject(RealtimeService); // runs the auth-reactive connection effect
  // BrandingStore loads and applies CSS variables on startup.
  private readonly branding = inject(BrandingStore);

  protected readonly nav = NAV_ITEMS;
  protected readonly displayName = computed(() => this.session.displayName());
  protected readonly notificationsOpen = signal(false);
  protected readonly assistantOpen = signal(false);
  protected readonly mobileMenuOpen = signal(false);

  constructor() {
    // Hydrate the inbox so a reload is not empty (FN-4).
    if (this.session.isAuthenticated()) {
      this.notificationApi
        .list(1, 50)
        .pipe(catchError(() => of(null)))
        .subscribe((page) => {
          if (page) {
            this.notifications.setAll(page.items.map(notificationFromDto));
          }
        });
    }
  }

  protected toggleNotifications(): void {
    this.notificationsOpen.update((v) => !v);
  }

  protected toggleAssistant(): void {
    this.assistantOpen.update((open) => !open);
  }

  protected toggleMobileMenu(): void {
    this.mobileMenuOpen.update((open) => !open);
  }

  protected closeMobileMenu(): void {
    this.mobileMenuOpen.set(false);
  }

  protected markRead(id: string): void {
    this.notificationApi.markRead(id).subscribe(() => this.notifications.markRead(id));
  }

  protected signOut(): void {
    this.session.signOut();
    void this.realtime.stop();
    void this.router.navigateByUrl('/login');
  }
}
