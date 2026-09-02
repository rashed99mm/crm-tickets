import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  ApiError,
  AsyncState,
  BrandingApi,
  BrandingDto,
  BrandingStore,
  CmsIntegrationApi,
  CsButton,
  CsCard,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsLoadingState,
  failed,
  fromList,
  loading,
  LocaleStore,
  PlatformSetting,
  PlatformSettingApi,
  TranslatePipe,
  TranslationKey,
} from 'common';

/**
 * FEAT-21 (US-803) — platform settings, editable. Frontend only: `PlatformSettingsController`
 * (list/create/update/delete) already exists, inherited with the platform.
 */
@Component({
  selector: 'admin-platform-settings',
  imports: [
    ReactiveFormsModule,
    CsCard,
    CsIcon,
    CsButton,
    CsLoadingState,
    CsEmptyState,
    CsErrorState,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './platform-settings.component.html',
})
export default class PlatformSettingsComponent {
  private readonly api = inject(PlatformSettingApi);
  private readonly brandingApi = inject(BrandingApi);
  private readonly brandingStore = inject(BrandingStore);
  private readonly cmsIntegration = inject(CmsIntegrationApi);

  protected readonly locale = inject(LocaleStore);

  /** Retained only for template compatibility with the hidden legacy block. */
  readonly routingRules: readonly { priority: number; conditionKey: TranslationKey; actionKey: TranslationKey }[] = [];

  readonly state = signal<AsyncState<readonly PlatformSetting[]>>(loading());
  readonly editingKey = signal<string | null>(null);
  readonly saving = signal(false);
  readonly saveError = signal<ApiError | null>(null);

  readonly brandingState = signal<AsyncState<BrandingDto | null>>(loading());
  readonly brandingSaveError = signal<ApiError | null>(null);
  readonly brandingSaving = signal(false);
  readonly erpImporting = signal(false);
  readonly erpImportMessage = signal<string | null>(null);
  readonly erpImportError = signal<string | null>(null);

  readonly brandingLoadError = computed<ApiError | null>(() => {
    const current = this.brandingState();
    return current.status === 'error' ? current.error : null;
  });

  readonly brandingForm = new FormGroup({
    logoUrl: new FormControl('', { nonNullable: true }),
    primaryColor: new FormControl('#2563EB', { nonNullable: true, validators: [Validators.pattern(/^#[0-9A-Fa-f]{6}$/)] }),
    accentColor: new FormControl('#2563EB', { nonNullable: true, validators: [Validators.pattern(/^#[0-9A-Fa-f]{6}$/)] }),
  });

  readonly items = computed<readonly PlatformSetting[]>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data : [];
  });

  readonly categories = computed(() =>
    [...new Set(this.items().map((setting) => setting.category).filter(Boolean))].sort(),
  );

  settingsFor(category: string): readonly PlatformSetting[] {
    return this.items().filter((setting) => setting.category === category);
  }

  readonly loadError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  readonly form = new FormGroup({
    value: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  constructor() {
    this.load();
    this.loadBranding();
  }

  refresh(): void {
    this.load();
    this.loadBranding();
  }

  loadBranding(): void {
    this.brandingState.set(loading());
    this.brandingApi.get().subscribe({
      next: (resp) => {
        if (resp.success && resp.data) {
          this.brandingState.set({ status: 'loaded', data: resp.data });
          this.brandingForm.patchValue({
            logoUrl: resp.data.logoUrl,
            primaryColor: resp.data.primaryColor,
            accentColor: resp.data.accentColor,
          });
        } else {
          this.brandingState.set({ status: 'loaded', data: null });
        }
      },
      error: (err: unknown) => this.brandingState.set(failed(this.toApiError(err))),
    });
  }

  saveBranding(): void {
    if (this.brandingForm.invalid || this.brandingSaving()) {
      this.brandingForm.markAllAsTouched();
      return;
    }
    this.brandingSaving.set(true);
    this.brandingSaveError.set(null);
    const val = this.brandingForm.getRawValue();
    this.brandingApi.update({ logoUrl: val.logoUrl, primaryColor: val.primaryColor, accentColor: val.accentColor }).subscribe({
      next: (resp) => {
        this.brandingSaving.set(false);
        if (resp.success && resp.data) {
          this.brandingState.set({ status: 'loaded', data: resp.data });
          this.brandingStore.branding.set(resp.data);
        }
      },
      error: (err: unknown) => {
        this.brandingSaving.set(false);
        this.brandingSaveError.set(this.toApiError(err));
      },
    });
  }

  useSelectedLogo(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    const reader = new FileReader();
    reader.addEventListener('load', () => {
      if (typeof reader.result === 'string') {
        this.brandingForm.controls.logoUrl.setValue(reader.result);
      }
    });
    reader.readAsDataURL(file);
  }

  clearLogo(): void {
    this.brandingForm.controls.logoUrl.setValue('');
  }

  discardBranding(): void {
    this.brandingSaveError.set(null);
    this.loadBranding();
  }

  importErpTickets(): void {
    if (this.erpImporting()) return;
    this.erpImporting.set(true);
    this.erpImportMessage.set(null);
    this.erpImportError.set(null);
    this.cmsIntegration.importErpTickets().subscribe({
      next: (result) => {
        this.erpImporting.set(false);
        this.erpImportMessage.set(`${result.imported} imported, ${result.skipped} already synced.`);
      },
      error: (error: unknown) => {
        this.erpImporting.set(false);
        this.erpImportError.set(error instanceof ApiError ? error.message_ : 'CMS ERP import failed.');
      },
    });
  }

  load(): void {
    this.state.set(loading());
    this.api.list().subscribe({
      // fromList only ever sees a SUCCESS payload, so an error can never be collapsed into "empty".
      next: (result) => this.state.set(fromList(result.items)),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  startEdit(setting: PlatformSetting): void {
    this.saveError.set(null);
    this.form.setValue({ value: setting.value });
    this.editingKey.set(setting.key);
  }

  cancelEdit(): void {
    this.editingKey.set(null);
    this.saveError.set(null);
  }

  save(setting: PlatformSetting): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.saveError.set(null);

    const { value } = this.form.getRawValue();

    // AC-803's numeric-value validation is server-side: the backend knows each setting's
    // ValueType, this screen does not duplicate that table client-side.
    this.api.update(setting.id, { value }).subscribe({
      next: () => {
        this.saving.set(false);
        this.editingKey.set(null);
        this.load();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.saveError.set(this.toApiError(error));
      },
    });
  }

  fieldError(field: string) {
    return this.saveError()?.fieldError(field) ?? null;
  }

  private toApiError(error: unknown): ApiError {
    return error instanceof ApiError
      ? error
      : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0);
  }
}
