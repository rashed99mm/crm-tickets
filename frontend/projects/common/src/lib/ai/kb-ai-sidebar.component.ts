import { ChangeDetectionStrategy, Component, input, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ContentsApi, ContentSummary, KbCategoryNode } from '../contents/contents.api';
import { AiChatPanelComponent } from './ai-chat-panel.component';
import { CsCard } from '../ui/card.component';
import { CsIcon } from '../ui/icon.component';
import { TranslatePipe } from '../i18n/translate.pipe';

const SUGGESTED_QUESTIONS = [
  'How do I reset my password?',
  'How do I update my billing information?',
  'How do I contact support?',
  'What are the ticket priority levels?',
  'How do I track my ticket status?',
];

@Component({
  selector: 'cs-kb-ai-sidebar',
  imports: [RouterLink, CsCard, CsIcon, AiChatPanelComponent, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './kb-ai-sidebar.component.html',
})
export class KbAiSidebarComponent {
  private readonly contentsApi = inject(ContentsApi);

  readonly article = input<ContentSummary | null>(null);
  readonly categories = signal<readonly KbCategoryNode[]>([]);

  constructor() {
    this.contentsApi.categories().subscribe({
      next: (cats) => this.categories.set(cats),
      error: () => this.categories.set([]),
    });
  }

  readonly rootCategories = computed(() =>
    this.categories().filter((c) => !c.parentId),
  );

  readonly suggestedQuestions = SUGGESTED_QUESTIONS;
}
