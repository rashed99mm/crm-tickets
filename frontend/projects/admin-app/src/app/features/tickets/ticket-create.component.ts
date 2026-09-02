import { ChangeDetectionStrategy, Component, computed, inject, signal, viewChild } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import {
  ApiError,
  CategoryOption,
  CsActionBar,
  CsAttachmentPicker,
  CsBadge,
  CsButton,
  CsCard,
  CsIcon,
  CsInputField,
  CustomerOption,
  derivePriority,
  LocaleStore,
  TICKET_IMPACTS,
  TICKET_URGENCIES,
  TicketApi,
  TicketImpact,
  TicketUrgency,
  TranslatePipe,
} from 'common';

/**
 * US-127 — the create-ticket form. `AC-59` (client rules mirror the server's) and `AC-60` (server
 * `errors[]` land on the control named by `field`).
 *
 * This is the screen the vertical-slice argument rests on: it is the first thing in the product to
 * consume a field-keyed server rejection, so it is where a wrong `errors[]` contract surfaces.
 */
@Component({
  selector: 'admin-ticket-create',
  imports: [ReactiveFormsModule, RouterLink, CsActionBar, CsCard, CsIcon, CsInputField, CsButton, CsBadge, CsAttachmentPicker, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ticket-create.component.html',
})
export default class TicketCreateComponent {
  private readonly api = inject(TicketApi);
  private readonly router = inject(Router);

  protected readonly locale = inject(LocaleStore);
  protected readonly impacts = TICKET_IMPACTS;
  protected readonly urgencies = TICKET_URGENCIES;

  readonly picker = viewChild(CsAttachmentPicker);

  readonly saving = signal(false);
  readonly submitError = signal<ApiError | null>(null);
  readonly customers = signal<readonly CustomerOption[]>([]);
  readonly categories = signal<readonly CategoryOption[]>([]);

  /** Files validated and pending in the shared picker — uploaded only after the ticket exists. */
  readonly pendingFiles = signal<readonly File[]>([]);
  readonly uploadingAttachments = signal(false);
  readonly attachmentError = signal<ApiError | null>(null);

  /**
   * Client rules mirroring the server's (AC-59) — 200 characters because
   * `CreateTicketCommandValidator` says 200, not because the input looked about right. Where the
   * two disagree the server wins, and AC-60's path is what shows the user why.
   */
  readonly form = new FormGroup({
    subject: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    customerId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    categoryId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    impact: new FormControl<TicketImpact>('Medium', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    urgency: new FormControl<TicketUrgency>('Medium', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  /**
   * AC-923.7 — a client-side mirror of the matrix, display only; the server derives the real
   * value. A plain writable signal, not `computed`: Reactive Forms values are not observed
   * reactively without an explicit subscription, so the constructor re-derives it on every
   * `impact`/`urgency` change.
   */
  readonly derivedPriority = signal<ReturnType<typeof derivePriority>>(derivePriority('Medium', 'Medium'));

  /** A failure with no field has no control to attach to, so it renders at form level (AC-60). */
  readonly formLevelError = computed(() => {
    const failure = this.submitError();
    return failure && !failure.hasFieldErrors ? failure : null;
  });

  constructor() {
    const recompute = () =>
      this.derivedPriority.set(
        derivePriority(this.form.controls.impact.value, this.form.controls.urgency.value),
      );
    this.form.controls.impact.valueChanges.subscribe(recompute);
    this.form.controls.urgency.valueChanges.subscribe(recompute);

    this.api.listCategories().subscribe({
      next: (categories) => this.categories.set(categories),
      // A picker that failed to load is not a form-level submit error, and showing it as one would
      // claim the submission failed before anything was submitted.
      error: () => this.categories.set([]),
    });

    this.api.searchCustomers('').subscribe({
      next: (page) => this.customers.set(page.items),
      error: () => this.customers.set([]),
    });
  }

  onFilesChange(files: readonly File[]): void {
    this.pendingFiles.set(files);
    this.attachmentError.set(null);
  }

  submit(): void {
    if (this.form.invalid || this.saving()) {
      // AC-59 — nothing leaves while the form is invalid, and nothing leaves twice.
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.submitError.set(null);

    this.api.create(this.form.getRawValue()).subscribe({
      next: (created) => {
        void this.uploadAttachmentsThenGo(created.id);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.submitError.set(this.toApiError(error));
      },
    });
  }

  /**
   * The ticket row exists; stream the pending files at it, then land on the queue.
   *
   * Uploading happens after create because the endpoint is keyed off a real ticket id. Best-effort
   * per file: one bad file must not strand the ticket already created or block the others.
   */
  private uploadAttachmentsThenGo(ticketId: string): void {
    const files = this.pendingFiles();
    if (files.length === 0) {
      this.picker()?.clear();
      this.saving.set(false);
      void this.router.navigateByUrl('/tickets');
      return;
    }

    this.uploadingAttachments.set(true);
    this.attachmentError.set(null);

    forkJoin(
      files.map((file) =>
        this.api.uploadAttachment(ticketId, file).pipe(
          catchError((error: unknown) => {
            this.attachmentError.set(this.toApiError(error));
            return of(null);
          }),
        ),
      ),
    ).subscribe({
      complete: () => {
        this.uploadingAttachments.set(false);
        this.picker()?.clear();
        this.saving.set(false);
        void this.router.navigateByUrl('/tickets');
      },
    });
  }

  /** AC-60 — the server error for one control, by the field name the server used. */
  fieldError(field: string) {
    return this.submitError()?.fieldError(field) ?? null;
  }

  /**
   * Clears a server error once the user edits the control it points at. Not spelled out by any
   * criterion — it is ordinary correctness. Leaving it would keep a corrected field showing the old
   * rejection, and the form would look broken.
   */
  clearServerError(field: string): void {
    const failure = this.submitError();
    if (!failure?.fieldError(field)) {
      return;
    }

    const remaining = failure.errors.filter((error) => error.field !== field);
    this.submitError.set(
      new ApiError(failure.code, failure.message_, remaining, failure.traceId, failure.status),
    );
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
