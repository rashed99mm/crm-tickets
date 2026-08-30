import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  ApiError,
  AsyncState,
  CsButton,
  CsDatePipe,
  CsDialog,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsInputField,
  CsLoadingState,
  CsPagination,
  LocaleStore,
  PagedResult,
  SessionStore,
  StaffApi,
  StaffUser,
  TranslatePipe,
  empty,
  failed,
  loaded,
  loading,
} from 'common';

type StatusTab = 'all' | 'active' | 'suspended';
/** Backend `sortBy` vocabulary (`IdentityUserService.GetUsersAsync`). */
type SortField = 'firstname' | 'email' | 'createdat';

/**
 * The seeded staff roles worth filtering an account list by. Matches `ApplicationRole.Roles`;
 * the platform's remaining roles (SuperAdmin, StateRepresentative, Visitor) are seed-time
 * personas, not accounts an administrator manages here.
 */
const STAFF_ROLES = ['Agent', 'Supervisor', 'Admin', 'SuperAdmin', 'ContentManager', 'User'] as const;

/**
 * Staff administration: the server-paged list, the create form, and activate/deactivate.
 *
 * Paging, search, status, role and sort are ALL server-side (`GET /api/Users` query params).
 * A staff list can be thousands of rows, so narrowing on the client would silently filter
 * only the single page already fetched — an "agents" view must reflect the whole result set.
 *
 * The list is an AsyncState union rather than an array plus a loading flag, so the template
 * cannot render an error as "no staff" (AUTH-18).
 */
@Component({
  selector: 'admin-users',
  imports: [
    CsDialog,
    CsIcon,
    ReactiveFormsModule,
    CsInputField,
    CsButton,
    CsLoadingState,
    CsEmptyState,
    CsErrorState,
    CsPagination,
    CsDatePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './users.component.html',
})
export default class UsersComponent {
  private readonly api = inject(StaffApi);
  private readonly session = inject(SessionStore);

  protected readonly locale = inject(LocaleStore);

  readonly state = signal<AsyncState<PagedResult<StaffUser>>>(loading());
  readonly saving = signal(false);
  readonly createError = signal<ApiError | null>(null);
  readonly showCreate = signal(false);

  // The server-side request state. Every change below resets the page and re-fetches.
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly totalCount = signal(0);
  readonly activeTab = signal<StatusTab>('all');
  readonly roleFilter = signal('');
  readonly searchTerm = signal('');
  readonly sortBy = signal<SortField>('createdat');
  readonly sortDirection = signal<'asc' | 'desc'>('desc');

  readonly roleOptions = STAFF_ROLES;
  readonly tabs: readonly StatusTab[] = ['all', 'active', 'suspended'];

  /** The active status tab as the backend's `isActive` predicate; `null` = all statuses. */
  readonly statusFilter = computed<boolean | null>(() => {
    const tab = this.activeTab();
    if (tab === 'active') return true;
    if (tab === 'suspended') return false;
    return null;
  });

  readonly items = computed<readonly StaffUser[]>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data.items : [];
  });

  readonly listError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  readonly hasMore = computed(
    () => this.items().length > 0 && this.page() * this.pageSize() < this.totalCount(),
  );

  readonly summaryRange = computed(() => {
    const total = this.totalCount();
    if (total === 0) return { start: 0, end: 0 };
    const start = (this.page() - 1) * this.pageSize() + 1;
    return { start, end: Math.min(this.page() * this.pageSize(), total) };
  });

  readonly isFiltered = computed(
    () =>
      this.searchTerm().trim().length > 0 ||
      this.roleFilter().length > 0 ||
      this.activeTab() !== 'all',
  );

  /** The signed-in user cannot deactivate themselves (AUTH-13). */
  readonly ownUserId = computed(() => this.session.userId());

  constructor() {
    this.load();
  }

  setTab(tab: StatusTab): void {
    this.activeTab.set(tab);
    this.page.set(1);
    this.load();
  }

  setRole(value: string): void {
    this.roleFilter.set(value);
    this.page.set(1);
    this.load();
  }

  onSearch(event: Event): void {
    this.searchTerm.set((event.target as HTMLInputElement).value);
  }

  submitSearch(): void {
    this.page.set(1);
    this.load();
  }

  clearSearch(): void {
    this.searchTerm.set('');
    this.page.set(1);
    this.load();
  }

  resetFilters(): void {
    this.searchTerm.set('');
    this.roleFilter.set('');
    this.activeTab.set('all');
    this.page.set(1);
    this.load();
  }

  sort(field: SortField): void {
    if (this.sortBy() === field) {
      this.sortDirection.set(this.sortDirection() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortBy.set(field);
      // Newest first and A–Z are the two "natural" defaults for their columns.
      this.sortDirection.set(field === 'createdat' ? 'desc' : 'asc');
    }
    this.page.set(1);
    this.load();
  }

  isSorted(field: SortField): boolean {
    return this.sortBy() === field;
  }

  ariaSort(field: SortField): 'ascending' | 'descending' | null {
    return this.isSorted(field) ? (this.sortDirection() === 'asc' ? 'ascending' : 'descending') : null;
  }

  goToPage(page: number): void {
    if (page < 1) {
      return;
    }
    this.page.set(page);
    this.load();
  }

  load(): void {
    this.state.set(loading());
    this.api
      .list({
        page: this.page(),
        pageSize: this.pageSize(),
        sortBy: this.sortBy(),
        sortDirection: this.sortDirection(),
        search: this.searchTerm().trim() || null,
        isActive: this.statusFilter(),
        role: this.roleFilter() || null,
      })
      .subscribe({
        // An error can never reach this branch — a failure arrives as ApiError and lands in
        // `state` as `error`, which is what keeps "server down with no data" distinct from
        // "genuinely no staff exist" (AC-416).
        next: (result) => {
          this.totalCount.set(result.totalCount);
          this.state.set(result.items.length === 0 ? empty() : loaded(result));
        },
        error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
      });
  }

  create(): void {
    if (this.form.invalid || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.createError.set(null);

    const { email, username, firstName, lastName, role, password } = this.form.getRawValue();

    this.api.create({ email, username, firstName, lastName, password, roles: [role] }).subscribe({
      next: () => {
        this.saving.set(false);
        this.form.reset({ role: 'User' });
        this.showCreate.set(false);
        this.load();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.createError.set(this.toApiError(error));
      },
    });
  }

  toggleActive(user: StaffUser): void {
    this.api.setActive(user.id, !user.isActive).subscribe({
      next: () => this.load(),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  exportCsv(): void {
    const rows = this.items().map((user) => [
      user.firstName,
      user.lastName,
      user.email,
      user.username,
      user.isActive ? 'Active' : 'Inactive',
      user.departmentName ?? '',
      user.roles.join('; '),
    ]);
    const csv = [
      ['First name', 'Last name', 'Email', 'Username', 'Status', 'Department', 'Roles'],
      ...rows,
    ].map((row) => row.map((cell) => `"${cell.replace(/"/g, '""')}"`).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = 'staff-users.csv';
    link.click();
    URL.revokeObjectURL(url);
  }

  /** Server field error for one control, so it lands on the right input (AUTH-19). */
  fieldError(field: string) {
    return this.createError()?.fieldError(field) ?? null;
  }

  readonly form = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email],
    }),
    username: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    firstName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    lastName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    // "User" is the ordinary staff role and "Admin" the administrative one —
    // the backend's actual two-role vocabulary (FE-2), not this feature's
    // earlier Supervisor/Agent naming.
    role: new FormControl('User', { nonNullable: true, validators: [Validators.required] }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(8)],
    }),
  });

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
