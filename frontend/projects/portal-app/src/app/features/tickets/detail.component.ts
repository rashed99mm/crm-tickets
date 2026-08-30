import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  ApiError,
  AsyncState,
  CsAttachmentList,
  CsBadge,
  CsCard,
  CsErrorState,
  CsIcon,
  CsLoadingState,
  CsStatusPill,
  failed,
  loaded,
  loading,
  LocaleStore,
  PortalApi,
  PortalTicketDetail,
  TranslatePipe,
} from 'common';

/** Read-only ticket view for the customer portal — description, the message timeline, reply and survey. */
@Component({
  selector: 'portal-ticket-detail',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    CsCard,
    CsIcon,
    CsBadge,
    CsStatusPill,
    CsLoadingState,
    CsErrorState,
    CsAttachmentList,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './detail.component.html',
})
export default class PortalTicketDetailComponent {
  private readonly api = inject(PortalApi);
  protected readonly locale = inject(LocaleStore);

  readonly id = input.required<string>();

  readonly state = signal<AsyncState<PortalTicketDetail>>(loading());

  readonly data = computed<PortalTicketDetail | null>(() => {
    const s = this.state();
    return s.status === 'loaded' ? s.data : null;
  });

  readonly error = computed<ApiError | null>(() => {
    const s = this.state();
    return s.status === 'error' ? s.error : null;
  });

  /** A reply is allowed until the ticket is closed (US-413, PJ-15). */
  readonly canReply = computed(() => {
    const d = this.data();
    return d != null && d.status !== 'Closed';
  });

  /** The survey opens once the ticket is resolved, and only once (US-408/US-409, PJ-11/16). */
  readonly canSurvey = computed(() => {
    const d = this.data();
    return d != null && !d.surveySubmitted && (d.status === 'Resolved' || d.status === 'Closed');
  });

  readonly replyForm = new FormGroup({
    body: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });
  readonly replyBody = signal('');
  readonly replyBusy = signal(false);
  readonly replyError = signal<ApiError | null>(null);

  readonly surveyRating = signal<number | null>(null);
  protected readonly surveyRatingOptions = [1, 2, 3, 4, 5] as const;
  readonly surveyComment = signal('');
  readonly comment = this.surveyComment;
  readonly surveyBusy = signal(false);
  readonly surveyError = signal<ApiError | null>(null);

  constructor() {
    effect(() => {
      const ticketId = this.id();
      this.loadTicket(ticketId);
    });
  }

  private loadTicket(ticketId: string): void {
    this.state.set(loading());
    this.api.getTicket(ticketId).subscribe({
      next: (detail) => this.state.set(loaded(detail)),
      error: (error: unknown) =>
        this.state.set(
          failed(error instanceof ApiError ? error : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0)),
        ),
    });
  }

  retry(): void {
    this.loadTicket(this.id());
  }

  submitReply(): void {
    if (this.replyForm.invalid || this.replyBusy()) {
      this.replyForm.markAllAsTouched();
      return;
    }

    this.replyBusy.set(true);
    this.replyError.set(null);

    const { body } = this.replyForm.getRawValue();
    this.api.reply(this.id(), body).subscribe({
      next: () => {
        this.replyBusy.set(false);
        this.replyForm.reset();
        this.replyBody.set('');
        this.loadTicket(this.id());
      },
      error: (failure: unknown) => {
        this.replyBusy.set(false);
        this.replyError.set(
          failure instanceof ApiError
            ? failure
            : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0),
        );
      },
    });
  }

  sendReply(): void {
    this.replyForm.controls.body.setValue(this.replyBody());
    this.submitReply();
  }

  pickRating(rating: number): void {
    this.surveyRating.set(rating);
    this.surveyError.set(null);
  }

  setRating(rating: number): void {
    this.pickRating(rating);
  }

  isSurveyRatingSelected(rating: number): boolean {
    return (this.surveyRating() ?? 0) >= rating;
  }

  isSurveyRatingCurrent(rating: number): boolean {
    return this.surveyRating() === rating;
  }

  surveyRatingButtonClass(rating: number): string {
    return this.isSurveyRatingSelected(rating)
      ? 'border-primary bg-primary text-on-primary'
      : 'border-outline-variant bg-surface-lowest text-on-surface';
  }

  submitSurvey(): void {
    if (this.surveyRating() == null || this.surveyBusy()) {
      return;
    }

    this.surveyBusy.set(true);
    this.surveyError.set(null);

    this.api
      .submitSurvey(this.id(), { rating: this.surveyRating()!, comment: this.surveyComment() || undefined })
      .subscribe({
        next: () => {
          this.surveyBusy.set(false);
          const current = this.state();
          if (current.status === 'loaded') {
            this.state.set(loaded({ ...current.data, surveySubmitted: true }));
          }
        },
        error: (failure: unknown) => {
          this.surveyBusy.set(false);
          this.surveyError.set(
            failure instanceof ApiError
              ? failure
              : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0),
          );
        },
      });
  }
}
