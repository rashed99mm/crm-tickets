import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  ApiError,
  AsyncState,
  CsCard,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsLoadingState,
  CsPagination,
  Customer,
  CustomerApi,
  initialsOf,
  PagedResult,
  empty,
  failed,
  loaded,
  loading,
  LocaleStore,
  TranslatePipe,
  CsDatePipe,
  CsDataToolbar,
} from 'common';

/** Kept as a constant because the page size and the has-more arithmetic must not drift apart. */
const PAGE_SIZE = 10;

/**
 * MVP-03 — the customer list. `AC-69`.
 *
 * Closes gap `G-5`: the customer API has been built and tested since Phase 2 and has been invisible
 * in the product, with customers reachable only as a `<select>` inside the ticket form.
 *
 * The list is an `AsyncState` union rather than an array plus a loading flag, for the same reason
 * the ticket queue is: with "data or nothing", `catchError(() => of([]))` looks reasonable and turns
 * a server outage into "no customers". `empty()` is set only from the success callback below, which
 * is what makes that mistake unrepresentable here.
 */
@Component({
  selector: 'admin-customer-list',
  imports: [
    RouterLink,
    CsCard,
    CsIcon,
    CsLoadingState,
    CsEmptyState,
    CsErrorState,
    CsPagination,
    TranslatePipe,

    CsDatePipe,
    CsDataToolbar,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './customer-list.component.html',
})
export default class CustomerListComponent {
  private readonly api = inject(CustomerApi);
  private readonly locale = inject(LocaleStore);

  readonly state = signal<AsyncState<PagedResult<Customer>>>(loading());
  readonly search = signal('');
  readonly page = signal(1);

  // Angular templates do not narrow a discriminated union across a @switch, so the two
  // payload-carrying cases are projected into typed signals here.
  readonly customers = computed<readonly Customer[]>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data.items : [];
  });

  readonly totalCount = computed(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data.totalCount : 0;
  });

  /**
   * The row's avatar mark. A method rather than a computed because it is per-row: a computed would
   * have to be one per customer, which is a signal graph the size of the page for a string
   * derivation. See `initialsOf` for why initials rather than a repeated glyph.
   */
  initials(name: string): string {
    return initialsOf(name);
  }

  readonly listError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  /**
   * "No customers recorded yet" under an active search is a lie — it says the database is empty
   * when the search simply matched nothing, and the user's next move is to go and create a record
   * that already exists. AC-69 names this distinction explicitly, so it is a criterion rather than
   * polish. The fix is copy, not logic.
   */
  readonly emptyMessage = computed(() =>
    this.locale.t(this.search() ? 'customers.empty.search' : 'customers.empty.all'),
  );

  readonly hasMore = computed(
    () => this.customers().length > 0 && this.page() * PAGE_SIZE < this.totalCount(),
  );

  constructor() {
    this.load();
  }

  load(): void {
    this.state.set(loading());

    this.api.list({ page: this.page(), pageSize: PAGE_SIZE, search: this.search() }).subscribe({
      // `empty` only ever describes a SUCCESSFUL request that returned nothing. An error can never
      // reach this branch, which is what keeps AC-69's two states distinct.
      next: (result) => this.state.set(result.items.length === 0 ? empty() : loaded(result)),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  /** A new search is a new result set, so it starts at page one rather than wherever paging left off. */
  applySearch(term: string): void {
    this.search.set(term.trim());
    this.page.set(1);
    this.load();
  }

  goToPage(page: number): void {
    if (page < 1) {
      return;
    }

    this.page.set(page);
    this.load();
  }

  private toApiError(error: unknown): ApiError {
    return error instanceof ApiError
      ? error
      : new ApiError(
          'ERR_UNKNOWN',
          'Something went wrong',
          [],
          '',
          0,
        );
  }
}
