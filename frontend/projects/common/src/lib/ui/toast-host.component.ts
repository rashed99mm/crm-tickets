import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslatePipe } from '../i18n/translate.pipe';
import { CsIcon } from './icon.component';
import { ToastKind, ToastService } from './toast.service';

@Component({
  selector: 'cs-toast-host',
  imports: [CsIcon, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './toast-host.component.html',
})
export class CsToastHost {
  readonly toasts = inject(ToastService);

  icon(kind: ToastKind): string {
    switch (kind) {
      case 'success':
        return 'check_circle';
      case 'error':
        return 'error';
      case 'warning':
        return 'warning';
      default:
        return 'info';
    }
  }

  tone(kind: ToastKind): string {
    switch (kind) {
      case 'success':
        return 'border-success/40 bg-success-container text-on-success-container shadow-[0_20px_50px_rgba(5,150,105,0.18)]';
      case 'error':
        return 'border-error/30 bg-error-container text-on-error-container';
      case 'warning':
        return 'border-status-waiting-for-customer/30 bg-status-waiting-for-customer/10 text-status-waiting-for-customer';
      default:
        return 'border-primary/30 bg-primary-container text-on-primary-container';
    }
  }
}
