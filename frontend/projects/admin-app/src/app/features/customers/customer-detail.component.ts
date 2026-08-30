import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  ApiError,
  AsyncState,
  CsButton,
  CsCard,
  CsErrorState,
  CsIcon,
  CsInputField,
  CsLoadingState,
  CsPlaceholder,
  Customer,
  CustomerApi,
  initialsOf,
  failed,
  loaded,
  loading,
  LocaleStore,
  TranslatePipe,
  CsDatePipe,
} from 'common';
import { CustomerAttachmentsComponent } from './customer-attachments.component';
import { CustomerNotesComponent } from './customer-notes.component';

/**
 * MVP-04 — one customer, their corrections, their removal, and their interaction history.
 * `AC-71`, `AC-72`, `AC-73`.
 *
 * Three concerns on one screen because they are one page to the agent. Notes and attachments are
 * child components, so `MVP-06` added the file list beside the history without touching the profile
 * or its edit form — and a failure in either child leaves the other working.
 */
@Component({
  selector: 'admin-customer-detail',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    CsCard,
    CsIcon,
    CsLoadingState,
    CsErrorState,
    CsInputField,
    CsButton,
    CsPlaceholder,
    CustomerNotesComponent,
    CustomerAttachmentsComponent,
    TranslatePipe,

    CsDatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './customer-detail.component.html',
})
export default class CustomerDetailComponent {
  private readonly api = inject(CustomerApi);
  private readonly router = inject(Router);

  protected readonly locale = inject(LocaleStore);

  /** Bound from the route via `withComponentInputBinding`. */
  readonly id = input.required<string>();

  readonly state = signal<AsyncState<Customer>>(loading());
  readonly editing = signal(false);
  readonly saving = signal(false);
  readonly saveError = signal<ApiError | null>(null);
  readonly confirmingDelete = signal(false);
  readonly deleting = signal(false);
  readonly deleteError = signal<ApiError | null>(null);

  /** Same rules as creation — AC-14 says the update validator is the create validator. */
  readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email, Validators.maxLength(320)],
    }),
    phone: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(32)],
    }),
  });

  readonly customer = computed<Customer | null>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data : null;
  });

  readonly loadError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  /**
   * The identity band's avatar mark. `customer_profile_history` puts a photograph here and nothing
   * in this product stores one, so the designed position holds initials — see `initialsOf` for why
   * that beats a generic glyph. A display derivation over the customer already in hand: it issues
   * nothing and changes no state (`AC-100`).
   */
  readonly initials = computed(() => initialsOf(this.customer()?.name));

  /**
   * AC-71 — an unknown id gets its own state.
   *
   * Note that a 404 stays an `error` in the union rather than becoming `empty()`: `empty` describes
   * a successful request that returned nothing, and letting a failure reach it is the exact
   * conflation the union exists to prevent. What differs is only what is *rendered* — a record that
   * genuinely does not exist deserves "no such customer" and a way back to the list, not a retry
   * button that will fail identically, and certainly not a blank edit form the agent could type
   * into.
   */
  readonly notFound = computed(() => this.loadError()?.status === 404);

  /** A save failure naming no field — a duplicate email — has no control to attach to (AC-72). */
  readonly formLevelError = computed(() => {
    const failure = this.saveError();
    return failure && !failure.hasFieldErrors ? failure : null;
  });

  constructor() {
    // Deliberately not in an effect: the id is a route input that does not change while this
    // component is alive, and an effect would re-fire on every unrelated signal write.
    queueMicrotask(() => this.load());
  }

  load(): void {
    this.state.set(loading());
    this.saveError.set(null);
    this.deleteError.set(null);

    this.api.get(this.id()).subscribe({
      next: (customer) => this.state.set(loaded(customer)),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  startEdit(): void {
    const current = this.customer();
    if (!current) {
      return;
    }

    this.saveError.set(null);
    this.form.setValue({
      name: current.name,
      email: current.email,
      phone: current.phone ?? '',
    });
    this.editing.set(true);
  }

  cancelEdit(): void {
    this.editing.set(false);
    this.saveError.set(null);
  }

  save(): void {
    const current = this.customer();
    if (!current || this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.saveError.set(null);

    const { name, email, phone } = this.form.getRawValue();

    this.api
      // "No phone" is null on the wire; an empty string would store a phone number that is not one.
      .update(current.id, { name, email, phone: phone.trim() === '' ? null : phone })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.editing.set(false);
          // AC-72 — the saved values come back from the server rather than being patched in
          // locally, so what is on screen is what persisted. A local patch would show the change
          // even where the server normalised or rejected part of it.
          this.refresh();
        },
        error: (error: unknown) => {
          this.saving.set(false);
          // AC-72 — the form stays open holding what the agent typed. A conflict means the change
          // was NOT applied, and closing the editor would suggest otherwise.
          this.saveError.set(this.toApiError(error));
        },
      });
  }

  /** AC-72 — the server error for one control, by the field name the server used. */
  fieldError(field: string) {
    return this.saveError()?.fieldError(field) ?? null;
  }

  clearServerError(field: string): void {
    const failure = this.saveError();
    if (!failure?.fieldError(field)) {
      return;
    }

    const remaining = failure.errors.filter((error) => error.field !== field);
    this.saveError.set(
      new ApiError(failure.code, failure.message_, remaining, failure.traceId, failure.status),
    );
  }

  /**
   * Removal is two steps rather than a `window.confirm`. A native dialog cannot be styled, cannot
   * be translated through the locale store, and is suppressed outright in some embedded browsers —
   * which would turn a guarded action into an unguarded one.
   */
  askToDelete(): void {
    this.deleteError.set(null);
    this.confirmingDelete.set(true);
  }

  cancelDelete(): void {
    this.confirmingDelete.set(false);
  }

  confirmDelete(): void {
    const current = this.customer();
    if (!current || this.deleting()) {
      return;
    }

    this.deleting.set(true);
    this.deleteError.set(null);

    this.api.remove(current.id).subscribe({
      next: () => {
        this.deleting.set(false);
        this.confirmingDelete.set(false);
        void this.router.navigateByUrl('/customers');
      },
      error: (error: unknown) => {
        this.deleting.set(false);
        this.confirmingDelete.set(false);
        // AC-73 — a customer holding tickets is refused with 409 (`CUSTOMER_HAS_TICKETS`). The
        // server's message is shown and the customer STAYS on screen: navigating away would
        // suggest the removal happened, and support history is not destroyable by one click.
        this.deleteError.set(this.toApiError(error));
      },
    });
  }

  /**
   * Re-reads without passing through `loading`.
   *
   * Setting `loading` would unmount the notes child and re-issue its request for every profile
   * save — the history is not what changed. The screen already holds a customer, so the honest
   * intermediate state is "the old values, briefly", not "nothing".
   */
  private refresh(): void {
    this.api.get(this.id()).subscribe({
      next: (customer) => this.state.set(loaded(customer)),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
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
