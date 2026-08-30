import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import {
  ApiError,
  AsyncState,
  CsButton,
  CsCard,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsLoadingState,
  CustomerApi,
  CustomerNote,
  PagedResult,
  empty,
  failed,
  loaded,
  loading,
  LocaleStore,
  TranslatePipe,
  CsDatePipe,
} from 'common';

/** The newest page. Notes are read far more often than they are scrolled back through. */
const PAGE_SIZE = 20;

/**
 * MVP-05 — a customer's interaction history. `AC-74` and `AC-75`.
 *
 * A child of the detail screen rather than part of it, so `MVP-06` can add attachments beside the
 * notes without touching the profile or its edit form.
 *
 * The client **never sends an author** (`AC-76`): `CustomerApi.addNote` takes a body and nothing
 * else, so there is no parameter here through which a caller could name one.
 */
@Component({
  selector: 'admin-customer-notes',
  imports: [
    CsCard,
    CsIcon,
    CsLoadingState,
    CsEmptyState,
    CsErrorState,
    CsButton,
    TranslatePipe,

    CsDatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './customer-notes.component.html',
})
export class CustomerNotesComponent {
  private readonly api = inject(CustomerApi);

  protected readonly locale = inject(LocaleStore);

  readonly customerId = input.required<string>();

  readonly state = signal<AsyncState<PagedResult<CustomerNote>>>(loading());
  readonly draft = signal('');
  readonly saving = signal(false);
  readonly submitError = signal<ApiError | null>(null);

  /**
   * Rendered in the order the server returned them. **Not re-sorted here**: the ordering is a
   * database index (`CreatedAt` descending), and a client-side sort would keep the list looking
   * correct while a server-side regression went unnoticed — including by the integration test that
   * exists to catch it.
   */
  readonly notes = computed<readonly CustomerNote[]>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data.items : [];
  });

  readonly loadError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  /** AC-75 — an empty or whitespace-only note is refused here, before any request is made. */
  readonly canSubmit = computed(() => this.draft().trim().length > 0 && !this.saving());

  constructor() {
    // Deliberately not in an effect: `customerId` is bound by the parent and does not change while
    // this component is alive, and an effect would re-fire on every unrelated signal write. It is
    // also not yet bound at construction, hence the microtask — the same reasoning as
    // `TicketDetailComponent`.
    queueMicrotask(() => this.load());
  }

  load(): void {
    this.state.set(loading());

    this.api.listNotes(this.customerId(), 1, PAGE_SIZE).subscribe({
      // `empty` only ever describes a SUCCESSFUL request that returned nothing. A failed read of
      // the history must never render as "no notes": an interaction record that silently appears
      // blank is worse than one that is plainly unavailable.
      next: (result) => this.state.set(result.items.length === 0 ? empty() : loaded(result)),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  updateDraft(value: string): void {
    this.draft.set(value);
  }

  add(): void {
    if (!this.canSubmit()) {
      // AC-75 — nothing leaves for an empty note, and nothing leaves twice.
      return;
    }

    this.saving.set(true);
    this.submitError.set(null);

    this.api.addNote(this.customerId(), this.draft().trim()).subscribe({
      next: () => {
        this.saving.set(false);
        this.draft.set('');
        // AC-75 — re-read rather than splice the new note in locally. The server owns the id, the
        // timestamp and the author name, and a locally constructed entry would have to invent all
        // three; it would also hide an ordering regression by placing the note where the client
        // expects it rather than where the server put it.
        this.load();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.submitError.set(this.toApiError(error));
      },
    });
  }

  /** The server keys a rejected note to `Body`; the interceptor lowercases it to `body`. */
  fieldError(field: string) {
    return this.submitError()?.fieldError(field) ?? null;
  }

  /** A failure naming no field — an unknown customer, an outage — has no control to attach to. */
  readonly formLevelError = computed(() => {
    const failure = this.submitError();
    return failure && !failure.hasFieldErrors ? failure : null;
  });

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
