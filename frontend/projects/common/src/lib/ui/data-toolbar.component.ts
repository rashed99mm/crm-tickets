import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { CsIcon } from './icon.component';

export interface DataToolbarOption {
  readonly value: string;
  readonly label: string;
}

@Component({
  selector: 'cs-data-toolbar',
  imports: [CsIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './data-toolbar.component.html',
})
export class CsDataToolbar {
  readonly search = input('');
  readonly searchPlaceholder = input('Search');
  readonly status = input('');
  readonly statusLabel = input('Status');
  readonly allStatusLabel = input('All statuses');
  readonly statusOptions = input<readonly DataToolbarOption[]>([]);
  readonly sort = input('');
  readonly sortLabel = input('Sort');
  readonly sortOptions = input<readonly DataToolbarOption[]>([]);

  readonly searchChanged = output<string>();
  readonly searchSubmitted = output<void>();
  readonly statusChanged = output<string>();
  readonly sortChanged = output<string>();
}
