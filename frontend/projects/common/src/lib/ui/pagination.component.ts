import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { TranslatePipe } from '../i18n/translate.pipe';
import { CsIcon } from './icon.component';

/**
 * Compact list footer for server-paged CRM tables.
 *
 * It owns the button shape and accessible labels so ticket, customer, report and admin lists do not
 * each re-create slightly different pagination controls.
 */
@Component({
  selector: 'cs-pagination',
  imports: [CsIcon, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './pagination.component.html',
})
export class CsPagination {
  readonly summary = input.required<string>();
  readonly page = input.required<number>();
  readonly hasMore = input.required<boolean>();
  readonly totalCount = input<number | null>(null);
  readonly pageSize = input(10);

  readonly previous = output<number>();
  readonly next = output<number>();
  readonly pageSelected = output<number>();

  readonly totalPages = computed(() => {
    const total = this.totalCount();
    return total === null ? null : Math.max(1, Math.ceil(total / this.pageSize()));
  });

  readonly pages = computed(() => {
    const total = this.totalPages();
    if (total === null) {
      return [this.page()];
    }

    const start = Math.max(1, Math.min(this.page() - 2, total - 4));
    const end = Math.min(total, start + 4);
    return Array.from({ length: end - start + 1 }, (_, index) => start + index);
  });

  selectPage(page: number): void {
    if (page === this.page()) {
      return;
    }

    this.pageSelected.emit(page);
  }
}
