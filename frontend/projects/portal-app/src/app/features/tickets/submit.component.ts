import { ChangeDetectionStrategy, Component, computed, inject, signal, viewChild } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import {
  ApiError,
  toLocalizedApiError,
  CategoryOption,
  CsAttachmentPicker,
  CsButton,
  CsIcon,
  FieldError,
  LocaleStore,
  PortalApi,
  TranslatePipe,
} from 'common';

/**
 * `submit_ticket` — the customer's raise-a-ticket form.
 *
 * Posts through `PortalApi.submitTicket`. The customer id is never sent: it is derived from the
 * signed-in session on the server (PJ-8), so the form has no customer picker — unlike the staff
 * create form, which chooses a customer on behalf of someone else.
 */
@Component({
  selector: 'portal-submit-ticket',
  imports: [ReactiveFormsModule, CsAttachmentPicker, CsButton, CsIcon, RouterLink, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './submit.component.html',
})
export default class PortalSubmitComponent {
  private readonly api = inject(PortalApi);
  private readonly router = inject(Router);
  protected readonly locale = inject(LocaleStore);

  readonly picker = viewChild(CsAttachmentPicker);


  readonly categories = signal<readonly CategoryOption[]>([]);
  readonly optionsLoading = signal(false);

  readonly form = new FormGroup({
    subject: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    categoryId: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  readonly busy = signal(false);
  readonly apiError = signal<ApiError | null>(null);

  /** Files validated and pending in the shared picker — uploaded only after the ticket exists. */
  readonly pendingFiles = signal<readonly File[]>([]);
  readonly uploadingAttachments = signal(false);
  readonly attachmentError = signal<ApiError | null>(null);

  private readonly fieldErrors = computed<FieldError[]>(() =>
    (this.apiError()?.errors ?? []).map((e) => ({
      field: e.field,
      code: e.code,
      message: e.message,
    })),
  );

  constructor() {
    this.optionsLoading.set(true);
    this.api
      .listCategories()
      .toPromise()
      .then((cats) => {
        this.categories.set(cats ?? []);
        this.optionsLoading.set(false);
      })
      .catch(() => this.optionsLoading.set(false));
  }

  fieldError(control: string): string | null {
    return this.fieldErrors().find((e) => e.field === control)?.message ?? null;
  }

  onFilesChange(files: readonly File[]): void {
    this.pendingFiles.set(files);
    this.attachmentError.set(null);
  }

  submit(): void {
    if (this.form.invalid || this.busy()) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.apiError.set(null);

    const { subject, categoryId, description } = this.form.getRawValue();

    // No priority: the server derives it from impact and urgency (US-923 / spec A2). The field it
    // used to be sent in does not exist on PortalCreateTicketRequest and was silently dropped.
    const payload = {
      subject,
      description,
      categoryId,
    };

    this.api.submitTicket(payload).subscribe({
      next: (created) => {
        void this.uploadAttachmentsThenGo(created.id);
      },

      error: (failure: unknown) => {
        this.busy.set(false);
        this.apiError.set(this.toApiError(failure));
      },
    });
  }

  /**
   * The ticket row exists; upload the pending files to it, then land on the list.
   *
   * Attachments upload after the ticket on purpose: the endpoint is keyed off a real ticket id, so
   * the form first secures the row, then streams files at it. Uploading is best-effort per file — a
   * failure on one must not strand the ticket already created or block the others.
   */
  private uploadAttachmentsThenGo(ticketId: string): void {
    const files = this.pendingFiles();
    if (files.length === 0) {
      this.picker()?.clear();
      this.busy.set(false);
      void this.router.navigateByUrl('/app/tickets');
      return;
    }

    this.uploadingAttachments.set(true);
    this.attachmentError.set(null);

    forkJoin(
      files.map((file) =>
        this.api.uploadTicketAttachment(ticketId, file).pipe(
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
        this.busy.set(false);
        void this.router.navigateByUrl('/app/tickets');
      },
    });
  }

  private toApiError(failure: unknown): ApiError {
    return toLocalizedApiError(failure, this.locale);
  }

  clearServerError(control: string): void {
    const current = this.apiError();
    if (!current) {
      return;
    }
    const remaining = this.fieldErrors().filter((e) => e.field !== control);
    this.apiError.set(
      remaining.length
        ? new ApiError(current.code, current.message_, remaining, current.traceId, current.status)
        : null,
    );
  }
}
