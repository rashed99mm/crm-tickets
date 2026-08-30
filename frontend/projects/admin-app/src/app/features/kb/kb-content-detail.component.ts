import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiError, ContentSummary, CsCard, CsDatePipe, CsIcon, KbAdminApi, TranslatePipe } from 'common';

@Component({
  selector: 'admin-kb-content-detail',
  imports: [RouterLink, CsCard, CsDatePipe, CsIcon, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="mx-auto flex max-w-5xl flex-col gap-6" data-design-system="command-center">
      <a routerLink="/kb-admin" class="inline-flex w-fit items-center gap-2 text-label-md text-primary hover:underline">
        <cs-icon name="arrow_back" [size]="18" /> {{ 'kb.detail.back' | t }}
      </a>
      @if (error(); as message) {
        <div class="rounded-xl border border-error/30 bg-error-container p-6 text-on-error-container">{{ message }}</div>
      } @else if (article(); as item) {
        <header class="rounded-2xl border border-outline-variant bg-surface-lowest p-6 shadow-card sm:p-10">
          <div class="flex flex-wrap items-center gap-2 text-label-sm uppercase tracking-widest text-primary">
            <span>{{ item.isFaq ? ('kb.form.faq' | t) : item.contentType }}</span>
            <span class="text-on-surface-variant">•</span>
            <span>{{ item.categoryName || item.category || ('field.notRecorded' | t) }}</span>
          </div>
          <h1 class="mt-4 max-w-4xl text-headline-xl text-on-surface">{{ item.title }}</h1>
          <p class="mt-4 max-w-3xl text-body-lg leading-8 text-on-surface-variant">{{ item.summary }}</p>
          <div class="mt-6 flex flex-wrap items-center gap-4 text-body-sm text-on-surface-variant">
            <span>{{ 'kb.admin.views' | t: item.viewCount }}</span>
            <span>{{ item.publishedAt ? (item.publishedAt | csDate) : ('kb.status.draft' | t) }}</span>
            <span class="rounded-full bg-surface-container px-3 py-1">{{ item.status }}</span>
          </div>
        </header>

        <div class="grid gap-6 lg:grid-cols-[minmax(0,1fr)_280px]">
          <article class="rounded-2xl border border-outline-variant bg-surface-lowest p-6 shadow-card sm:p-10">
            <div class="whitespace-pre-wrap text-body-lg leading-9 text-on-surface">{{ item.body }}</div>
          </article>
          <aside class="flex flex-col gap-4">
            <cs-card [heading]="'kb.detail.automation' | t">
              <div class="space-y-3 p-4 text-body-sm leading-6 text-on-surface-variant">
                <p>Use this content to ground Pulse AI answers and suggested replies.</p>
                <p>Clear steps help agents resolve tickets faster and help SLA automation choose the next action.</p>
              </div>
            </cs-card>
            <cs-card [heading]="'kb.admin.contentStrategy' | t">
              <div class="p-4 text-body-sm leading-6 text-on-surface-variant">{{ item.tags.join(' • ') }}</div>
            </cs-card>
          </aside>
        </div>
      } @else {
        <div class="rounded-2xl border border-outline-variant bg-surface-lowest p-10 text-center text-on-surface-variant">{{ 'kb.loading' | t }}</div>
      }
    </section>
  `,
})
export default class KbContentDetailComponent {
  private readonly api = inject(KbAdminApi);
  private readonly route = inject(ActivatedRoute);
  readonly article = signal<ContentSummary | null>(null);
  readonly error = signal<string | null>(null);

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Content id is missing.');
      return;
    }
    this.api.get(id).subscribe({
      next: (item) => this.article.set(item),
      error: (failure: unknown) => this.error.set(failure instanceof ApiError ? failure.message_ : 'Unable to load content.'),
    });
  }
}
