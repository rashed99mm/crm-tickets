import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'cs-action-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class:
      'sticky bottom-0 z-10 flex min-h-14 flex-wrap items-center justify-end gap-2 border-t border-border-subtle bg-surface-lowest/95 px-4 py-3 backdrop-blur',
  },
  templateUrl: './action-bar.component.html',
})
export class CsActionBar {}
