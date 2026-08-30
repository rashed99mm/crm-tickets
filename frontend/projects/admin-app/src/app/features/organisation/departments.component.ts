import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  ApiError,
  AsyncState,
  CsButton,
  CsCard,
  CsDialog,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsInputField,
  CsLoadingState,
  Department,
  DepartmentApi,
  failed,
  fromList,
  loading,
  LocaleStore,
  TranslatePipe,
} from 'common';

/**
 * FEAT-16 (US-309) — department administration: the list, the create form, and deactivation.
 * `AC-119`, `AC-120`, `AC-121`.
 *
 * Mirrors `UsersComponent`'s shape: an `AsyncState` union so a failed read can never render as
 * "no departments" (the same rule `AUTH-18` states for staff).
 */
@Component({
  selector: 'admin-departments',
  imports: [
    CsCard,
    CsDialog,
    CsIcon,
    ReactiveFormsModule,
    CsInputField,
    CsButton,
    CsLoadingState,
    CsEmptyState,
    CsErrorState,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './departments.component.html',
})
export default class DepartmentsComponent {
  private readonly api = inject(DepartmentApi);

  protected readonly locale = inject(LocaleStore);

  readonly state = signal<AsyncState<readonly Department[]>>(loading());
  readonly saving = signal(false);
  readonly createError = signal<ApiError | null>(null);
  readonly showCreate = signal(false);

  readonly items = computed<readonly Department[]>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data : [];
  });

  readonly listError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.state.set(loading());
    this.api.list().subscribe({
      // fromList only ever sees a SUCCESS payload, so an error can never be collapsed into "empty".
      next: (result) => this.state.set(fromList(result.items)),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  create(): void {
    if (this.form.invalid || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.createError.set(null);

    const { name } = this.form.getRawValue();

    this.api.create({ name }).subscribe({
      next: () => {
        this.saving.set(false);
        this.form.reset();
        this.showCreate.set(false);
        this.load();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.createError.set(this.toApiError(error));
      },
    });
  }

  deactivate(department: Department): void {
    this.api.deactivate(department.id).subscribe({
      next: () => this.load(),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  /** Server field error for one control, so it lands on the right input. */
  fieldError(field: string) {
    return this.createError()?.fieldError(field) ?? null;
  }

  private toApiError(error: unknown): ApiError {
    return error instanceof ApiError
      ? error
      : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0);
  }
}
