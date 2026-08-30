import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  ApiError,
  AsyncState,
  BusinessHoursCalendar,
  CsButton,
  CsCard,
  CsDialog,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsInputField,
  CsLoadingState,
  failed,
  fromList,
  loading,
  LocaleStore,
  PublicHoliday,
  SLAPolicy,
  SLAPolicyApi,
  TICKET_PRIORITIES,
  TranslatePipe,
} from 'common';

type SLATab = 'policies' | 'businessHours' | 'holidays';

/**
 * FEAT-17 (US-214, and the US-223 addendum) — SLA policy administration: list, create, edit and
 * deactivation. Mirrors `DepartmentsComponent`'s shape — an `AsyncState` union so a failed read
 * never renders as "no policies".
 */
@Component({
  selector: 'admin-sla-policies',
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
  templateUrl: './sla-policies.component.html',
})
export default class SLAPoliciesComponent {
  private readonly api = inject(SLAPolicyApi);

  protected readonly locale = inject(LocaleStore);
  protected readonly priorities = TICKET_PRIORITIES;

  readonly state = signal<AsyncState<readonly SLAPolicy[]>>(loading());
  readonly businessHoursState = signal<AsyncState<readonly BusinessHoursCalendar[]>>(loading());
  readonly holidayState = signal<AsyncState<readonly PublicHoliday[]>>(loading());
  readonly saving = signal(false);
  readonly calendarSaving = signal(false);
  readonly holidaySaving = signal(false);
  readonly createError = signal<ApiError | null>(null);
  readonly calendarError = signal<ApiError | null>(null);
  readonly holidayError = signal<ApiError | null>(null);
  readonly showCreate = signal(false);
  readonly showCalendarCreate = signal(false);
  readonly showHolidayCreate = signal(false);
  readonly activeTab = signal<SLATab>('policies');

  readonly items = computed<readonly SLAPolicy[]>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data : [];
  });

  readonly activePolicyCount = computed(() => this.items().filter((policy) => policy.isActive).length);
  readonly escalationRuleCount = computed(() => this.items().filter((policy) => policy.isActive).length * 3);

  readonly listError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  readonly businessHoursError = computed<ApiError | null>(() => {
    const current = this.businessHoursState();
    return current.status === 'error' ? current.error : null;
  });

  readonly holidayListError = computed<ApiError | null>(() => {
    const current = this.holidayState();
    return current.status === 'error' ? current.error : null;
  });

  readonly businessHours = computed<readonly BusinessHoursCalendar[]>(() => {
    const current = this.businessHoursState();
    return current.status === 'loaded' ? current.data : [];
  });

  readonly holidays = computed<readonly PublicHoliday[]>(() => {
    const current = this.holidayState();
    return current.status === 'loaded' ? current.data : [];
  });

  readonly form = new FormGroup({
    priority: new FormControl('Normal', { nonNullable: true, validators: [Validators.required] }),
    responseTargetHours: new FormControl(4, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0.1)],
    }),
    resolutionTargetHours: new FormControl(24, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0.1)],
    }),
  });

  readonly calendarForm = new FormGroup({
    branchId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    dayOfWeek: new FormControl('Monday', { nonNullable: true, validators: [Validators.required] }),
    openTime: new FormControl('09:00', { nonNullable: true, validators: [Validators.required] }),
    closeTime: new FormControl('17:00', { nonNullable: true, validators: [Validators.required] }),
  });

  readonly holidayForm = new FormGroup({
    branchId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    holidayDate: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  constructor() {
    this.load();
    this.loadBusinessHours();
    this.loadHolidays();
  }

  selectTab(tab: SLATab): void {
    this.activeTab.set(tab);
  }

  openActiveCreate(): void {
    switch (this.activeTab()) {
      case 'businessHours':
        this.showCalendarCreate.set(true);
        break;
      case 'holidays':
        this.showHolidayCreate.set(true);
        break;
      default:
        this.showCreate.set(true);
        break;
    }
  }

  load(): void {
    this.state.set(loading());
    this.api.list().subscribe({
      // fromList only ever sees a SUCCESS payload, so an error can never be collapsed into "empty".
      next: (result) => this.state.set(fromList(result.items)),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  loadBusinessHours(): void {
    this.businessHoursState.set(loading());
    this.api.listBusinessHours().subscribe({
      next: (result) => this.businessHoursState.set(fromList(result.items)),
      error: (error: unknown) => this.businessHoursState.set(failed(this.toApiError(error))),
    });
  }

  loadHolidays(): void {
    this.holidayState.set(loading());
    this.api.listHolidays().subscribe({
      next: (result) => this.holidayState.set(fromList(result.items)),
      error: (error: unknown) => this.holidayState.set(failed(this.toApiError(error))),
    });
  }

  create(): void {
    if (this.form.invalid || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.createError.set(null);

    const { priority, responseTargetHours, resolutionTargetHours } = this.form.getRawValue();

    this.api
      .create({
        priority: priority as (typeof TICKET_PRIORITIES)[number],
        responseTargetHours,
        resolutionTargetHours,
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.form.reset({ priority: 'Normal', responseTargetHours: 4, resolutionTargetHours: 24 });
          this.showCreate.set(false);
          this.load();
        },
        error: (error: unknown) => {
          this.saving.set(false);
          this.createError.set(this.toApiError(error));
        },
      });
  }

  createBusinessHours(): void {
    if (this.calendarForm.invalid || this.calendarSaving()) {
      return;
    }

    this.calendarSaving.set(true);
    this.calendarError.set(null);
    this.api.createBusinessHours(this.calendarForm.getRawValue()).subscribe({
      next: () => {
        this.calendarSaving.set(false);
        this.showCalendarCreate.set(false);
        this.activeTab.set('businessHours');
        this.calendarForm.reset({ branchId: '', dayOfWeek: 'Monday', openTime: '09:00', closeTime: '17:00' });
        this.loadBusinessHours();
      },
      error: (error: unknown) => {
        this.calendarSaving.set(false);
        this.calendarError.set(this.toApiError(error));
      },
    });
  }

  createHoliday(): void {
    if (this.holidayForm.invalid || this.holidaySaving()) {
      return;
    }

    this.holidaySaving.set(true);
    this.holidayError.set(null);
    this.api.createHoliday(this.holidayForm.getRawValue()).subscribe({
      next: () => {
        this.holidaySaving.set(false);
        this.showHolidayCreate.set(false);
        this.activeTab.set('holidays');
        this.holidayForm.reset({ branchId: '', holidayDate: '', name: '' });
        this.loadHolidays();
      },
      error: (error: unknown) => {
        this.holidaySaving.set(false);
        this.holidayError.set(this.toApiError(error));
      },
    });
  }

  deactivate(policy: SLAPolicy): void {
    this.api.deactivate(policy.id).subscribe({
      next: () => this.load(),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  fieldError(field: string) {
    return this.createError()?.fieldError(field) ?? null;
  }

  readonly editingId = signal<string | null>(null);
  readonly editSaving = signal(false);
  readonly editError = signal<ApiError | null>(null);

  readonly editForm = new FormGroup({
    priority: new FormControl('Normal', { nonNullable: true, validators: [Validators.required] }),
    responseTargetHours: new FormControl(4, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0.1)],
    }),
    resolutionTargetHours: new FormControl(24, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0.1)],
    }),
  });

  startEdit(policy: SLAPolicy): void {
    this.editingId.set(policy.id);
    this.editError.set(null);
    this.editForm.setValue({
      priority: policy.priority,
      responseTargetHours: policy.responseTargetHours,
      resolutionTargetHours: policy.resolutionTargetHours,
    });
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.editError.set(null);
  }

  saveEdit(): void {
    const id = this.editingId();
    if (!id || this.editForm.invalid || this.editSaving()) {
      return;
    }

    this.editSaving.set(true);
    this.editError.set(null);

    const { priority, responseTargetHours, resolutionTargetHours } = this.editForm.getRawValue();

    this.api
      .update(id, {
        priority: priority as (typeof TICKET_PRIORITIES)[number],
        responseTargetHours,
        resolutionTargetHours,
      })
      .subscribe({
        next: () => {
          this.editSaving.set(false);
          this.editingId.set(null);
          this.load();
        },
        error: (error: unknown) => {
          this.editSaving.set(false);
          this.editError.set(this.toApiError(error));
        },
      });
  }

  editFieldError(field: string) {
    return this.editError()?.fieldError(field) ?? null;
  }

  private toApiError(error: unknown): ApiError {
    return error instanceof ApiError
      ? error
      : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0);
  }
}
