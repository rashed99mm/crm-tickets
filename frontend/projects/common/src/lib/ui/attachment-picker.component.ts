import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import {
  ALLOWED_TICKET_ATTACHMENT_TYPES,
  MAX_TICKET_ATTACHMENT_BYTES,
} from '../tickets/ticket.api';
import { CsIcon } from './icon.component';
import { LocaleStore } from '../i18n/locale.store';
import { TranslatePipe } from '../i18n/translate.pipe';

/**
 * A size readable at a glance. `sizeBytes` is exact and unreadable — the same convention as the
 * customer attachments list ("9.4 MB", "94 MB"), in binary units because the 10 MB limit is
 * expressed in them.
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

  return `${value < 10 ? value.toFixed(1) : Math.round(value)} ${units[unit]}`;
}

interface PickerEntry {
  readonly file: File;
  /** An object URL for the browser to render; empty for files that cannot be previewed. */
  readonly url: string;
  readonly previewable: boolean;
}

/**
 * A reusable file picker for the ticket forms (TA-1/TA-9): select, validate against the SAME limits
 * the server enforces, preview images, and remove before upload.
 *
 * It only *collects* pending files — it has no ticket id, so it cannot upload. That is the parent's
 * job, after the ticket row exists. `filesChange` emits the validated selection whenever it changes;
 * the parent uploads them and owns the busy state.
 */
@Component({
  selector: 'cs-attachment-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, CsIcon],
  template: `
    <input
      #fileInput
      type="file"
      multiple
      [accept]="accept"
      class="hidden"
      (change)="chooseFile($event)"
    />

    <button type="button" class="cs-btn cs-btn-ghost inline-flex items-center gap-2" (click)="fileInput.click()">
      <cs-icon name="attach_file" [size]="18" />
      {{ 'attachments.choose' | t }}
    </button>

    <p class="cs-hint mt-1">{{ limitHint() }}</p>

    @if (refusal()) {
      <p class="cs-error mt-1">{{ refusal() }}</p>
    }

    @if (entries().length > 0) {
      <ul class="mt-3 flex flex-col gap-2">
        @for (entry of entries(); track entry.file) {
          <li class="flex items-center gap-2">
            @if (entry.previewable) {
              <img [src]="entry.url" class="h-10 w-10 rounded object-cover" alt="" />
            } @else {
              <cs-icon name="attachment" class="h-5 w-5 text-neutral-400" />
            }
            <span class="min-w-0 flex-1">
              <span class="block truncate text-sm">{{ entry.file.name }}</span>
              <span class="text-xs text-neutral-500">{{ format(entry.file.size) }}</span>
            </span>
            <button
              type="button"
              class="cs-btn cs-btn-ghost cs-btn-sm"
              [attr.aria-label]="'action.remove' | t"
              (click)="remove(entry.file)"
            >
              {{ 'action.remove' | t }}
            </button>
          </li>
        }
      </ul>
    }
  `,
})
export class CsAttachmentPicker {
  private readonly locale = inject(LocaleStore);

  /** The byte cap of this picker. Defaults to the ticket attachment limit. */
  readonly maxBytes = input(MAX_TICKET_ATTACHMENT_BYTES);

  /** Emits the validated selection whenever it changes. */
  readonly filesChange = output<readonly File[]>();

  /** Filters the dialog to the allowlist — a courtesy; `refuse()` still checks, and so does the server. */
  readonly accept = ALLOWED_TICKET_ATTACHMENT_TYPES.join(',');

  readonly entries = signal<readonly PickerEntry[]>([]);

  /** A refusal the CLIENT made — a plain string, because no server said it. */
  readonly refusal = signal<string | null>(null);

  readonly files = computed<readonly File[]>(() => this.entries().map((entry) => entry.file));

  readonly limitHint = computed(() =>
    this.locale.t('attachments.limitHint', formatBytes(this.maxBytes())),
  );

  /** Exposes the shared formatter to the template for per-row sizes. */
  format(bytes: number): string {
    return formatBytes(bytes);
  }

  /** Mirrors the customer-attachments refusal, checked in the server's own order (size, then type). */
  refuse(file: File): string | null {
    if (file.size > this.maxBytes()) {
      return this.locale.t(
        'attachments.tooLarge',
        file.name,
        formatBytes(file.size),
        formatBytes(this.maxBytes()),
      );
    }

    if (!(ALLOWED_TICKET_ATTACHMENT_TYPES as readonly string[]).includes(file.type)) {
      const described =
        file.type === ''
          ? this.locale.t('attachments.typeUnrecognised')
          : this.locale.t('attachments.typeNamed', file.type);
      return this.locale.t('attachments.wrongType', file.name, described);
    }

    return null;
  }

  /** Clears the selection and revokes every object URL. */
  clear(): void {
    this.revokeAll();
    this.entries.set([]);
    this.refusal.set(null);
    this.filesChange.emit([]);
  }

  private revokeAll(): void {
    for (const entry of this.entries()) {
      if (entry.url) {
        URL.revokeObjectURL(entry.url);
      }
    }
  }

  chooseFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const picked = Array.from(input.files ?? []);
    // Clearing lets the SAME file be chosen again — a browser reports no `change` on an identical
    // value, which would silently swallow a second pick.
    input.value = '';

    if (picked.length === 0) {
      return;
    }

    const firstRefused = picked.map((file) => this.refuse(file)).find((reason) => reason !== null);
    // The refused file is not added; the acceptable rest still are. Rejecting the whole selection
    // on one bad file would make the user re-pick the good half.
    const accepted = picked.filter((file) => this.refuse(file) === null);

    this.refusal.set(firstRefused ?? null);
    this.filesChange.emit([...this.files(), ...accepted]);
    this.entries.set([
      ...this.entries(),
      ...accepted.map((file) => ({
        file,
        url: file.type.startsWith('image/') ? URL.createObjectURL(file) : '',
        previewable: file.type.startsWith('image/'),
      })),
    ]);
  }

  remove(file: File): void {
    const removed = this.entries().find((entry) => entry.file === file);
    if (removed?.url) {
      URL.revokeObjectURL(removed.url);
    }

    const remaining = this.entries().filter((entry) => entry.file !== file);
    this.entries.set(remaining);
    this.filesChange.emit(remaining.map((entry) => entry.file));
  }
}
