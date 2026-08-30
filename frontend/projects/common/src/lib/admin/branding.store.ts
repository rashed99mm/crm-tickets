import { effect, Injectable, inject, signal } from '@angular/core';
import { BrandingApi, BrandingDto } from './branding.api';

const FALLBACK: BrandingDto = {
  logoUrl: '',
  primaryColor: '#2563EB',
  accentColor: '#2563EB',
};

@Injectable({ providedIn: 'root' })
export class BrandingStore {
  private readonly api = inject(BrandingApi);

  readonly branding = signal<BrandingDto>(FALLBACK);

  constructor() {
    this.api.get().subscribe({
      next: (resp) => {
        if (resp.success && resp.data) {
          this.branding.set(resp.data);
        }
      },
      error: () => {
        // Non-fatal: use defaults.
      },
    });

    effect(() => {
      const b = this.branding();
      const root = document.documentElement;
      root.style.setProperty('--primary-color', b.primaryColor);
      root.style.setProperty('--accent-color', b.accentColor);
      root.style.setProperty('--brand-logo-url', b.logoUrl ? `url("${b.logoUrl}")` : '');
    });
  }
}
