import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  ApiError,
  AsyncState,
  CsCard,
  CsButton,
  CsEmptyState,
  CsIcon,
  CsInputField,
  CsLoadingState,
  CsStatusPill,
  empty,
  failed,
  loaded,
  loading,
  PortalApi,
  PortalTicketListItem,
  SessionStore,
  StaffProfile,
  TranslatePipe,
} from 'common';

@Component({
  selector: 'portal-profile',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    CsCard,
    CsButton,
    CsInputField,
    CsIcon,
    CsLoadingState,
    CsEmptyState,
    CsStatusPill,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './profile.component.html',
})
export default class PortalProfileComponent implements OnInit {
  protected readonly session = inject(SessionStore);
  private readonly api = inject(PortalApi);

  readonly activeTab = signal<'info' | 'history'>('info');
  readonly profile = signal<StaffProfile | null>(null);
  readonly profileLoading = signal(true);
  readonly profileBusy = signal(false);
  readonly profileSaved = signal(false);
  readonly profileError = signal<ApiError | null>(null);
  readonly selectedImage = signal<string | null>(null);
  readonly imageUrl = computed(() => this.selectedImage() ?? this.profile()?.profileImageUrl ?? null);
  readonly profileForm = new FormGroup({
    firstName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
    lastName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
    phoneNumber: new FormControl<string | null>(null),
    profileImageUrl: new FormControl<string | null>(null),
  });
  readonly history = signal<AsyncState<readonly PortalTicketListItem[]>>(loading());
  readonly historyItems = computed(() => {
    const state = this.history();
    return state.status === 'loaded' ? state.data : [];
  });

  constructor() {
    this.loadHistory();
  }

  ngOnInit(): void {
    this.loadProfile();
  }

  loadProfile(): void {
    this.profileLoading.set(true);
    this.api.getProfile().subscribe({
      next: (profile) => {
        this.profile.set(profile);
        this.profileForm.patchValue({
          firstName: profile.firstName,
          lastName: profile.lastName,
          phoneNumber: profile.phoneNumber ?? null,
          profileImageUrl: profile.profileImageUrl ?? null,
        });
        this.profileLoading.set(false);
      },
      error: (error: unknown) => {
        this.profileLoading.set(false);
        this.profileError.set(error instanceof ApiError ? error : new ApiError('ERR_UNKNOWN', 'Unable to load profile', [], '', 0));
      },
    });
  }

  saveProfile(): void {
    if (this.profileForm.invalid || this.profileBusy()) return;
    this.profileBusy.set(true);
    this.profileSaved.set(false);
    this.api.updateProfile(this.profileForm.getRawValue()).subscribe({
      next: (profile) => {
        this.profile.set(profile);
        this.profileBusy.set(false);
        this.profileSaved.set(true);
      },
      error: (error: unknown) => {
        this.profileBusy.set(false);
        this.profileError.set(error instanceof ApiError ? error : new ApiError('ERR_UNKNOWN', 'Unable to save profile', [], '', 0));
      },
    });
  }

  onImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || !file.type.startsWith('image/')) return;
    const reader = new FileReader();
    reader.onload = () => {
      const imageUrl = typeof reader.result === 'string' ? reader.result : null;
      this.selectedImage.set(imageUrl);
      this.profileForm.controls.profileImageUrl.setValue(imageUrl);
    };
    reader.readAsDataURL(file);
  }

  removeImage(): void {
    this.selectedImage.set(null);
    this.profileForm.controls.profileImageUrl.setValue(null);
  }

  cancelEdit(): void {
    const current = this.profile();
    if (!current) return;
    this.profileForm.patchValue({
      firstName: current.firstName,
      lastName: current.lastName,
      phoneNumber: current.phoneNumber ?? null,
      profileImageUrl: current.profileImageUrl ?? null,
    });
    this.selectedImage.set(null);
    this.profileSaved.set(false);
    this.profileError.set(null);
  }

  setTab(tab: 'info' | 'history'): void {
    this.activeTab.set(tab);
  }

  loadHistory(): void {
    this.history.set(loading());
    this.api.listTickets().subscribe({
      next: (tickets) => this.history.set(tickets.length === 0 ? empty() : loaded(tickets)),
      error: (error: unknown) =>
        this.history.set(
          failed(
            error instanceof ApiError
              ? error
              : new ApiError('ERR_UNKNOWN', 'Unable to load history', [], '', 0),
          ),
        ),
    });
  }
}
