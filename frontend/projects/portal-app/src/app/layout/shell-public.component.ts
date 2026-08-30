import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import {
  CsIcon,
  CsLanguageSwitcher,
  LocaleStore,
  SessionStore,
  TranslatePipe,
} from 'common';

@Component({
  selector: 'portal-public-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, CsIcon, CsLanguageSwitcher, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell-public.component.html',
})
export class PortalPublicShell {
  private readonly router = inject(Router);
  protected readonly session = inject(SessionStore);
  protected readonly locale = inject(LocaleStore);

  protected readonly isAuthenticated = this.session.isAuthenticated;
  protected readonly displayName = this.session.displayName;
  protected readonly authOnlyLayout = signal(this.isAuthPath(this.router.url));

  constructor() {
    this.router.events.subscribe((event) => {
      if (event instanceof NavigationEnd) {
        this.authOnlyLayout.set(this.isAuthPath(event.urlAfterRedirects));
      }
    });
  }

  protected signOut(): void {
    this.session.signOut();
  }

  private isAuthPath(url: string): boolean {
    const pathname = new URL(url, 'http://localhost').pathname;
    return pathname === '/login' || pathname === '/signup';
  }
}
