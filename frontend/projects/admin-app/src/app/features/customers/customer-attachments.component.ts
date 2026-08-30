import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import {
  ALLOWED_ATTACHMENT_TYPES,
  ApiError,
  AsyncState,
  CsButton,
  CsCard,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsLoadingState,
  CustomerApi,
  CustomerAttachment,
  PagedResult,
  empty,
  failed,
  loaded,
  loading,
  LocaleStore,
  MAX_ATTACHMENT_BYTES,
  TranslatePipe,
} from 'common';

/** The newest page, matching the notes beside it. Files are read far more often than paged through. */
const PAGE_SIZE = 20;

/**
 * A size an agent can judge at a glance. `sizeBytes` is exact and unreadable — "1048576" tells
 * nobody whether the file is large, and `AC-83` asks for the size to be *listed*, which means
 * legible rather than merely present.
 *
 * Binary units (1 KB = 1024 B) because that is what the 10 MB limit is expressed in; mixing the
 * decimal convention here would let a file report 10.4 MB and still be accepted.
 */
export function formatBytes(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }

  const units = ['KB', 'MB', 'GB'];
  let value = bytes / 1024;
  let unit = 0;

  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit += 1;
  }

  // One decimal below 10, none above: "9.4 MB" is useful, "94.3 MB" is noise.
  return `${value < 10 ? value.toFixed(1) : Math.round(value)} ${units[unit]}`;
}

/**
 * MVP-06 — a customer's attachments. `AC-83`, `AC-84`, `AC-85`.
 *
 * A sibling of `CustomerNotesComponent` rather than part of it, so the detail screen composes two
 * independent children: a failure in either one leaves the other working, and neither re-reads when
 * the other changes.
 */
@Component({
  selector: 'admin-customer-attachments',
  imports: [
    CsCard,
    CsIcon,
    CsLoadingState,
    CsEmptyState,
    CsErrorState,
    CsButton,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './customer-attachments.component.html',
})
export class CustomerAttachmentsComponent {
  private readonly api = inject(CustomerApi);

  protected readonly locale = inject(LocaleStore);

  readonly customerId = input.required<string>();

  readonly state = signal<AsyncState<PagedResult<CustomerAttachment>>>(loading());

  readonly uploading = signal(false);
  readonly uploadError = signal<ApiError | null>(null);

  /**
   * A refusal the CLIENT made, before any request — deliberately a plain string and not an
   * `ApiError`, because no server said it. Conflating the two would make it possible to render a
   * local guess as though the server had spoken.
   */
  readonly refusal = signal<string | null>(null);

  readonly confirmingRemovalOf = signal<string | null>(null);
  readonly removing = signal(false);
  readonly removeError = signal<ApiError | null>(null);

  readonly downloadingId = signal<string | null>(null);
  readonly downloadError = signal<ApiError | null>(null);

  /** Rendered in the order the server returned them, for the same reason the notes list is. */
  readonly attachments = computed<readonly CustomerAttachment[]>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data.items : [];
  });

  readonly loadError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  /** Shown beside the picker so the rules are visible before a file is chosen, not only after. */
  readonly limitHint = computed(() =>
    this.locale.t('attachments.limitHint', formatBytes(MAX_ATTACHMENT_BYTES)),
  );

  /**
   * Filters the file dialog to the allowlist — a third courtesy, weaker than the two that follow
   * it: `accept` is a hint the user can override in every browser's dialog, so `refuse()` still
   * checks, and the server still checks after that. Derived from the same constant so the three
   * cannot drift.
   */
  readonly accept = ALLOWED_ATTACHMENT_TYPES.join(',');

  constructor() {
    // Same reasoning as the notes child: `customerId` is bound by the parent and does not change
    // while this component is alive, so an effect would only re-fire on unrelated signal writes.
    // The microtask lets the binding land before the read.
    queueMicrotask(() => this.load());
  }

  load(): void {
    this.state.set(loading());

    this.api.listAttachments(this.customerId(), 1, PAGE_SIZE).subscribe({
      // `empty` describes a SUCCESSFUL request that returned nothing, and is reachable from here
      // only. A failed read must never render as "no attachments": evidence that silently appears
      // absent is worse than evidence that is plainly unavailable.
      next: (result) => this.state.set(result.items.length === 0 ? empty() : loaded(result)),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  /**
   * The `<input type="file">` handler.
   *
   * The input is cleared afterwards so choosing the *same* file twice still fires a `change` — a
   * browser reports no change when the value is identical, which would silently swallow a retry
   * after a failed upload.
   */
  chooseFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    input.value = '';

    if (file) {
      this.upload(file);
    }
  }

  /**
   * AC-84's local half.
   *
   * Returns the reason this file cannot be sent, naming **which** rule refused it — "too large" and
   * "wrong type" call for different corrections, and a single "invalid file" message makes the user
   * guess. Checked in the server's own order (size, then type) so the two agree about which rule a
   * file trips first.
   *
   * This is a courtesy that saves a 10 MB round trip. `AC-23` and `AC-24` are enforced by the
   * server independently and remain the control.
   */
  refuse(file: File): string | null {
    if (file.size > MAX_ATTACHMENT_BYTES) {
      return this.locale.t(
        'attachments.tooLarge',
        file.name,
        formatBytes(file.size),
        formatBytes(MAX_ATTACHMENT_BYTES),
      );
    }

    if (!(ALLOWED_ATTACHMENT_TYPES as readonly string[]).includes(file.type)) {
      // An unrecognised extension arrives with an empty `type`, which is correctly not on the list.
      const described =
        file.type === ''
          ? this.locale.t('attachments.typeUnrecognised')
          : this.locale.t('attachments.typeNamed', file.type);
      return this.locale.t('attachments.wrongType', file.name, described);
    }

    return null;
  }

  upload(file: File): void {
    if (this.uploading()) {
      return;
    }

    this.uploadError.set(null);

    const refused = this.refuse(file);
    if (refused) {
      // Nothing leaves. The request is never issued, so the round trip is never spent.
      this.refusal.set(refused);
      return;
    }

    this.refusal.set(null);
    this.uploading.set(true);

    this.api.uploadAttachment(this.customerId(), file).subscribe({
      next: () => {
        this.uploading.set(false);
        // AC-84 — re-read rather than splice the row in locally. The server owns the id, the
        // stored name, the uploader and the timestamp; a locally built row would invent all four,
        // and would keep looking right if the write had partly failed.
        this.load();
      },
      error: (error: unknown) => {
        this.uploading.set(false);
        // Including the server's own refusal of a file this client happened to accept — the two
        // checks are independent, and this is what the user sees when they disagree.
        this.uploadError.set(this.toApiError(error));
      },
    });
  }

  /**
   * AC-85 — the bytes, then a synthetic anchor.
   *
   * Not an `<a href>`: the content route requires a session and a link carries no `Authorization`
   * header, so the obvious implementation 401s and reads as a broken button. `HttpClient` puts the
   * request through the auth interceptor, and the blob is handed to the browser from memory.
   */
  download(attachment: CustomerAttachment): void {
    if (this.downloadingId()) {
      return;
    }

    this.downloadingId.set(attachment.id);
    this.downloadError.set(null);

    this.api.downloadAttachment(this.customerId(), attachment.id).subscribe({
      next: (blob) => {
        this.downloadingId.set(null);
        this.save(blob, attachment.originalFileName);
      },
      error: (error: unknown) => {
        this.downloadingId.set(null);
        this.downloadError.set(this.toApiError(error));
      },
    });
  }

  askToRemove(id: string): void {
    this.removeError.set(null);
    this.confirmingRemovalOf.set(id);
  }

  cancelRemove(): void {
    this.confirmingRemovalOf.set(null);
  }

  /**
   * AC-85 — removal is confirmed in the page, not through `window.confirm`, for the same reasons
   * the customer's own removal is: a native dialog cannot be styled or translated, and some
   * embedded browsers suppress it entirely, turning a guarded action into an unguarded one.
   */
  confirmRemove(id: string): void {
    if (this.removing()) {
      return;
    }

    this.removing.set(true);
    this.removeError.set(null);

    this.api.removeAttachment(this.customerId(), id).subscribe({
      next: () => {
        this.removing.set(false);
        this.confirmingRemovalOf.set(null);
        // Re-read, so what is on screen is what the server still holds.
        this.load();
      },
      error: (error: unknown) => {
        this.removing.set(false);
        this.confirmingRemovalOf.set(null);
        // The row stays. Navigating it away would suggest the file was deleted when it was not.
        this.removeError.set(this.toApiError(error));
      },
    });
  }

  displaySize(attachment: CustomerAttachment): string {
    return formatBytes(attachment.sizeBytes);
  }

  /**
   * The mockup's `1.2 MB • Oct 12` line.
   *
   * Built through the dictionary rather than concatenated in the template: the bullet is visible
   * text between two values, and `no-hardcoded-strings` scans exactly that. Keeping the whole
   * pattern in `attachments.meta` also lets a translator move the separator, which a template
   * literal could not.
   *
   * The date is formatted here with the same `Intl` locale the `csDate` pipe uses, so the rail's
   * dates and the timeline's agree.
   */
  sizeAndDate(attachment: CustomerAttachment): string {
    const on = new Date(attachment.createdAt).toLocaleDateString(this.locale.locale(), {
      day: 'numeric',
      month: 'short',
    });

    return this.locale.t('attachments.meta', formatBytes(attachment.sizeBytes), on);
  }

  /**
   * The file's glyph, by content type — the mockup's `picture_as_pdf` / `description` / `table`
   * row markers.
   *
   * A lookup over the five types the allowlist admits, with a generic fallback rather than a throw:
   * the server's allowlist can grow before this map does, and an unrecognised type is a paperclip,
   * not a crash.
   */
  glyph(attachment: CustomerAttachment): string {
    switch (attachment.contentType) {
      case 'application/pdf':
        return 'picture_as_pdf';
      case 'image/png':
      case 'image/jpeg':
      case 'image/gif':
        return 'image';
      case 'text/plain':
        return 'description';
      default:
        return 'attach_file';
    }
  }

  /**
   * The glyph's colour.
   *
   * Literal class strings, for the reason `cs-badge` documents at length: Tailwind emits a rule
   * only for a class it can find in the source text, so a colour assembled at runtime would be
   * styled under `ng serve` and colourless in the production build.
   */
  glyphTone(attachment: CustomerAttachment): string {
    switch (attachment.contentType) {
      case 'application/pdf':
        return 'text-error';
      case 'image/png':
      case 'image/jpeg':
      case 'image/gif':
        return 'text-status-open';
      default:
        return 'text-on-surface-variant';
    }
  }

  private save(blob: Blob, fileName: string): void {
    const objectUrl = URL.createObjectURL(blob);

    const anchor = document.createElement('a');
    anchor.href = objectUrl;
    anchor.download = fileName;
    anchor.style.display = 'none';
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();

    // Revoked on the next macrotask, not synchronously: revoking in the same tick can cancel a
    // download the browser has not started reading yet. Not revoking at all leaks the blob for the
    // lifetime of the document, which for a page an agent keeps open all day is a real cost.
    setTimeout(() => URL.revokeObjectURL(objectUrl), 0);
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
