import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ApiError } from '../api/api-error';
import { CsIcon } from './icon.component';
import { LocaleStore } from '../i18n/locale.store';
import { PortalApi, PortalTicketAttachment } from '../portal/portal.api';
import { TicketApi, TicketAttachment } from '../tickets/ticket.api';
import { TranslatePipe } from '../i18n/translate.pipe';

type Mode = 'staff' | 'portal';

interface AttachmentViewModel {
  readonly id: string;
  readonly originalFileName: string;
  readonly contentType: string;
  readonly createdAt: string;
  /** Object URL for image previews (fetched with the auth header), or null for files not previewed. */
  readonly url: string | null;
}

/**
 * The attachment list on a ticket detail (TA-5/TA-6 staff, TA-7 portal), shared so the two surfaces
 * cannot drift.
 *
 * Previews are rendered from blob object URLs, never a bare `src`: the content route needs the auth
 * header that only `HttpClient` supplies, and a link carries no `Authorization`. Each image is
 * fetched and its bytes handed to the browser from memory.
 */
@Component({
  selector: 'cs-attachment-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, CsIcon],
  template: `
    <h3 class="mb-3 font-headline-md text-on-surface">{{ 'attachments.title' | t }}</h3>

    @if (loading()) {
      <p class="text-body-sm text-on-surface-variant">{{ 'attachments.loading' | t }}</p>
    } @else if (error(); as failure) {
      <p role="alert" class="text-body-sm text-error">{{ failure.message_ }}</p>
    } @else if (attachments().length === 0) {
      <p class="text-body-sm text-on-surface-variant">{{ 'attachments.emptyTicket' | t }}</p>
    } @else {
      <ul class="flex flex-col gap-3">
        @for (attachment of attachments(); track attachment.id) {
          <li class="flex items-center gap-3">
            @if (attachment.url) {
              <img
                [src]="attachment.url"
                class="h-12 w-12 rounded object-cover"
                [alt]="attachment.originalFileName"
              />
            } @else {
              <cs-icon name="attachment" class="h-5 w-5 text-neutral-400" />
            }
            <div class="min-w-0 flex-1">
              <p class="truncate text-body-sm text-on-surface">{{ attachment.originalFileName }}</p>
              <p class="text-body-sm text-on-surface-variant">{{ attachment.createdAt }}</p>
            </div>
            <button
              type="button"
              class="rounded-lg bg-primary px-3 py-1.5 text-label-md font-semibold text-on-primary transition-all hover:opacity-90 disabled:opacity-60"
              (click)="download(attachment)"
              [disabled]="downloadingId() !== null"
            >
              {{
                downloadingId() === attachment.id
                  ? ('attachments.uploading' | t)
                  : ('action.download' | t)
              }}
            </button>
          </li>
        }
      </ul>
      @if (downloadError(); as failure) {
        <p role="alert" class="mt-2 text-body-sm text-error">{{ failure.message_ }}</p>
      }
    }
  `,
})
export class CsAttachmentList {
  private readonly portalApi = inject(PortalApi);
  private readonly ticketApi = inject(TicketApi);
  private readonly locale = inject(LocaleStore);
  private readonly destroyRef = inject(DestroyRef);

  /** Which backing endpoint to read from. */
  readonly mode = input<Mode>('staff');

  readonly ticketId = input.required<string>();

  readonly loading = signal(true);
  readonly error = signal<ApiError | null>(null);
  readonly attachments = signal<readonly AttachmentViewModel[]>([]);
  readonly downloadingId = signal<string | null>(null);
  readonly downloadError = signal<ApiError | null>(null);

  constructor() {
    this.destroyRef.onDestroy(() => this.revoke());

    effect(() => {
      const ticketId = this.ticketId();
      const mode = this.mode();
      untracked(() => this.load(ticketId, mode));
    });
  }

  private load(ticketId: string, mode: Mode): void {
    this.loading.set(true);
    this.error.set(null);
    this.revoke();

    const source = mode === 'portal'
      ? this.portalApi.listTicketAttachments(ticketId)
      : this.ticketApi.listAttachments(ticketId);

    source.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (list) => {
        this.attachments.set(
          list.map((attachment) => ({
            id: attachment.id,
            originalFileName: attachment.originalFileName,
            contentType: attachment.contentType,
            createdAt: attachment.createdAt,
            url: null as string | null,
          })),
        );
        this.loading.set(false);
        this.previewImages(ticketId, list);
      },
      error: (failure: unknown) => {
        this.loading.set(false);
        this.error.set(this.toApiError(failure));
      },
    });
  }

  private previewImages(ticketId: string, list: readonly (TicketAttachment | PortalTicketAttachment)[]): void {
    for (const attachment of list) {
      if (!attachment.contentType.startsWith('image/')) {
        continue;
      }
      const bytes = this.mode() === 'portal'
        ? this.portalApi.downloadTicketAttachment(ticketId, attachment.id)
        : this.ticketApi.downloadAttachment(ticketId, attachment.id);

      bytes.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: (blob) => this.setPreviewUrl(attachment.id, URL.createObjectURL(blob)),
        error: () => undefined,
      });
    }
  }

  private setPreviewUrl(attachmentId: string, url: string): void {
    this.attachments.set(
      this.attachments().map((attachment) =>
        attachment.id === attachmentId ? { ...attachment, url } : attachment,
      ),
    );
  }

  download(attachment: AttachmentViewModel): void {
    if (this.downloadingId()) {
      return;
    }

    this.downloadingId.set(attachment.id);
    this.downloadError.set(null);

    const bytes = this.mode() === 'portal'
      ? this.portalApi.downloadTicketAttachment(this.ticketId(), attachment.id)
      : this.ticketApi.downloadAttachment(this.ticketId(), attachment.id);

    bytes.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (blob) => {
        this.downloadingId.set(null);
        this.save(blob, attachment.originalFileName);
      },
      error: (failure: unknown) => {
        this.downloadingId.set(null);
        this.downloadError.set(this.toApiError(failure));
      },
    });
  }

  private save(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  private revoke(): void {
    for (const attachment of this.attachments()) {
      if (attachment.url) {
        URL.revokeObjectURL(attachment.url);
      }
    }
    this.attachments.set([]);
  }

  private toApiError(failure: unknown): ApiError {
    return failure instanceof ApiError
      ? failure
      : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0);
  }
}
