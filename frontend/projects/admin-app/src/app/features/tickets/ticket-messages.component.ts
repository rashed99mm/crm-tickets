import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import {
  ApiError,
  AsyncState,
  AiApi,
  CsButton,
  CsCard,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsLoadingState,
  MESSAGE_CHANNELS,
  MESSAGE_DIRECTIONS,
  MessageChannel,
  MessageDirection,
  getChannelTranslationKey,
  TicketApi,
  TicketMessage,
  empty,
  failed,
  loaded,
  loading,
  LocaleStore,
  TranslatePipe,
  CsDatePipe,
} from 'common';

/**
 * FEAT-14 — a ticket's conversation record. `AC-106` through `AC-114`.
 *
 * A child of the detail screen, the same arrangement `CustomerNotesComponent` uses beside the
 * customer profile: an independent load/failure cycle, so a broken message timeline never takes
 * the ticket's status actions down with it.
 */
@Component({
  selector: 'admin-ticket-messages',
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
  templateUrl: './ticket-messages.component.html',
})
export class TicketMessagesComponent {
  private readonly api = inject(TicketApi);
  private readonly ai = inject(AiApi);

  protected readonly locale = inject(LocaleStore);
  protected readonly directions = MESSAGE_DIRECTIONS;
  protected readonly channels = MESSAGE_CHANNELS;

  protected channelKey(channel: MessageChannel) {
    return getChannelTranslationKey(channel);
  }

  readonly ticketId = input.required<string>();

  readonly state = signal<AsyncState<readonly TicketMessage[]>>(loading());
  readonly direction = signal<MessageDirection>('Outbound');
  readonly channel = signal<MessageChannel>('System');
  readonly subject = signal('');
  readonly body = signal('');
  readonly saving = signal(false);
  readonly submitError = signal<ApiError | null>(null);

  // AC-F13 — Draft with AI toolbar button. `aiAvailable` mirrors the rail's A1 rule:
  // a single ERR052 flips it off, the button hides, and the rest of the composer keeps working.
  readonly drafting = signal(false);
  readonly aiAvailable = signal(true);

  /** Oldest first, exactly as the server returns them — no client-side re-sort (AC-106). */
  readonly messages = computed<readonly TicketMessage[]>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data : [];
  });

  readonly loadError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  /** AC-113 — an empty or whitespace-only body is refused here, before any request is made. */
  readonly canSubmit = computed(() => this.body().trim().length > 0 && !this.saving());

  constructor() {
    // Deliberately not in an effect: `ticketId` is bound by the parent and does not change while
    // this component is alive — the same reasoning `CustomerNotesComponent` and
    // `TicketDetailComponent` both give for the identical pattern.
    queueMicrotask(() => this.load());
  }

  load(): void {
    this.state.set(loading());

    this.api.listMessages(this.ticketId()).subscribe({
      // `empty` only ever describes a SUCCESSFUL request that returned nothing (AC-111). A failed
      // read must never render as "no messages" — that would hide a real outage as an honest fact.
      next: (result) => this.state.set(result.length === 0 ? empty() : loaded(result)),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  setDirection(value: string): void {
    this.direction.set(value as MessageDirection);
  }

  setChannel(value: string): void {
    this.channel.set(value as MessageChannel);
  }

  updateSubject(value: string): void {
    this.subject.set(value);
  }

  updateBody(value: string): void {
    this.body.set(value);
  }

  log(): void {
    if (!this.canSubmit()) {
      // AC-113 — nothing leaves for an empty message.
      return;
    }

    this.saving.set(true);
    this.submitError.set(null);

    const subject = this.subject().trim();

    this.api
      .recordMessage(this.ticketId(), {
        direction: this.direction(),
        channel: this.channel(),
        subject: subject === '' ? undefined : subject,
        body: this.body().trim(),
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.subject.set('');
          this.body.set('');
          // AC-112 — re-read rather than splice the new message in locally: the server owns the id,
          // the timestamp and the sender name, none of which this form has.
          this.load();
        },
        error: (error: unknown) => {
          this.saving.set(false);
          // AC-114 — the timeline is untouched; nothing was optimistically added to it.
          this.submitError.set(this.toApiError(error));
        },
      });
  }

  // AC-F13 / AC-F14 — the composer toolbar's Draft with AI and the right-rail card's Insert
  // both write into the body signal. The card's Insert calls `insertDraft(text)` directly via
  // a parent/child template reference. Both paths leave the existing recordMessage flow alone.
  insertDraft(text: string): void {
    this.body.set(text);
    this.submitError.set(null);
  }

  draftWithAi(): void {
    if (this.drafting() || !this.aiAvailable()) {
      return;
    }

    this.drafting.set(true);
    this.ai.draftReply(this.ticketId()).subscribe({
      next: (suggestion) => {
        const payload = suggestion.payload as { drafts?: unknown } | undefined;
        const first = Array.isArray(payload?.drafts)
          ? payload.drafts.find(
              (d): d is string => typeof d === 'string' && d.length > 0,
            )
          : undefined;
        if (first) {
          this.insertDraft(first);
        }
        this.drafting.set(false);
      },
      error: (e: unknown) => {
        // A1 — a deployment-level ERR052 flips the affordance off permanently. A transient
        // provider error (e.g. AI_PROVIDER_FAILED) leaves the button visible for a retry.
        if (e instanceof ApiError && e.code === 'ERR052') {
          this.aiAvailable.set(false);
        }
        this.drafting.set(false);
      },
    });
  }

  /** The server keys a rejected message to `Body`; the interceptor lowercases it to `body`. */
  fieldError(field: string) {
    return this.submitError()?.fieldError(field) ?? null;
  }

  /** A failure naming no field — an unknown ticket, an outage — has no control to attach to. */
  readonly formLevelError = computed(() => {
    const failure = this.submitError();
    return failure && !failure.hasFieldErrors ? failure : null;
  });

  private toApiError(error: unknown): ApiError {
    return error instanceof ApiError
      ? error
      : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0);
  }
}
