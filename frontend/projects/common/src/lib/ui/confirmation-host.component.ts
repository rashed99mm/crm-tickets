import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslatePipe } from '../i18n/translate.pipe';
import { ConfirmationService } from './confirmation.service';
import { CsIcon } from './icon.component';

@Component({
  selector: 'cs-confirmation-host',
  imports: [CsIcon, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './confirmation-host.component.html',
})
export class CsConfirmationHost {
  readonly confirmations = inject(ConfirmationService);
}
