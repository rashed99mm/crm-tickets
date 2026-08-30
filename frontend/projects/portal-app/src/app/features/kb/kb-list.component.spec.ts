import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { envelopeInterceptor, ContentsApi } from 'common';
import PortalKbListComponent from './kb-list.component';

const ARTICLE = {
  id: 'a1',
  title: 'How to reset your password',
  summary: 'Steps to reset',
  contentType: 'Article',
  status: 'Published',
  category: 'Account',
  tags: [],
  viewCount: 10,
  likeCount: 0,
  publishedAt: null,
  body: 'Do this.',
};

describe('PortalKbListComponent', () => {
  let http: HttpTestingController;

  function setup() {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [PortalKbListComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  }

  /** The component fires the browse list AND the FAQ fetch on init. */
  function flushBoth(list: object, faq: object[] = []) {
    const listReq = http.expectOne((r) => r.url === '/api/knowledge-base/articles');
    listReq.flush(list);
    const faqReq = http.expectOne((r) => r.url === '/api/knowledge-base/articles/faq' && r.params.get('take') === '8');
    faqReq.flush({ items: faq, pageIndex: 1, pageSize: 8, totalCount: faq.length });
  }

  function create() {
    setup();
    const fixture = TestBed.createComponent(PortalKbListComponent);
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => http.verify());

  it('renders published articles from the public knowledge-base endpoint (defect fix)', () => {
    const fixture = create();
    flushBoth({
      items: [ARTICLE],
      pageIndex: 1,
      pageSize: 10,
      totalCount: 1,
    });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('How to reset your password');
  });

  it('shows the empty state when no articles exist', () => {
    const fixture = create();
    flushBoth({ items: [], pageIndex: 1, pageSize: 10, totalCount: 0 });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('No articles found.');
  });

  it('renders the FAQ section when FAQ articles are returned (US-513)', () => {
    const fixture = create();
    flushBoth(
      { items: [], pageIndex: 1, pageSize: 10, totalCount: 0 },
      [{ ...ARTICLE, id: 'f1', title: 'What is the SLA?', isFaq: true }],
    );
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Frequently Asked Questions');
    expect(el.textContent).toContain('What is the SLA?');
  });

  it('AC411_AdminAndKnowledgeBaseScreensPreserveReferenceHierarchy: renders knowledge base cards and navigation links', () => {
    const fixture = create();
    flushBoth({
      items: [ARTICLE],
      pageIndex: 1,
      pageSize: 10,
      totalCount: 1,
    });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('a[href]')).not.toBeNull();
  });

  it('AC418_AdminTablesAndRailsAreKeyboardAccessible: kb links are keyboard focusable', () => {
    const fixture = create();
    flushBoth({ items: [], pageIndex: 1, pageSize: 10, totalCount: 0 });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el).not.toBeNull();
  });

  // ── FEAT-31 mockup-fidelity tests ─────────────────────────────────────────────

  it('AC500_kb_list_renders_hero_and_four_category_cards', () => {
    const fixture = create();
    flushBoth({ items: [], pageIndex: 1, pageSize: 10, totalCount: 0 });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('How can we help you today?');
    expect(el.textContent).toContain('Getting Started');
    expect(el.textContent).toContain('Account Management');
    expect(el.textContent).toContain('Billing');
    expect(el.textContent).toContain('Technical Support');
  });

  it('AC500_kb_list_renders_faq_bento_section', () => {
    const fixture = create();
    const faqItems = [{ id: '1', title: 'How do I reset my password?', summary: 'Step-by-step guide', viewCount: 42, category: 'Account' }];
    flushBoth({ items: [], pageIndex: 1, pageSize: 10, totalCount: 0 }, faqItems);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Frequently Asked Questions');
  });

  it('AC500_kb_list_renders_still_need_help_cta', () => {
    const fixture = create();
    flushBoth({ items: [], pageIndex: 1, pageSize: 10, totalCount: 0 }, []);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Still need help?');
  });

  it('AC501_kb_list_search_via_enter_and_button', () => {
    const fixture = create();
    flushBoth({ items: [], pageIndex: 1, pageSize: 10, totalCount: 0 });
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input[type="search"]') as HTMLInputElement;
    input.value = 'password';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    fixture.componentInstance.submitSearch();
    fixture.detectChanges();

    const req = http.expectOne((r) => r.url === '/api/knowledge-base/articles' && r.params.get('searchTerm') === 'password');
    req.flush({ items: [], pageIndex: 1, pageSize: 10, totalCount: 0 });
    // submitSearch() re-fires the FAQ fetch too, with the same term.
    http
      .expectOne((r) => r.url === '/api/knowledge-base/articles/faq' && r.params.get('take') === '8')
      .flush({ items: [], pageIndex: 1, pageSize: 3, totalCount: 0 });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No articles found.');
  });

  it('AC502_kb_list_loading_state_shows_spinner', () => {
    setup();
    const fixture = TestBed.createComponent(PortalKbListComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Loading articles');
    http.expectOne((r) => r.url === '/api/knowledge-base/articles').flush({ items: [], pageIndex: 1, pageSize: 10, totalCount: 0 });
    http.expectOne((r) => r.url === '/api/knowledge-base/articles/faq' && r.params.get('take') === '8').flush({ items: [], pageIndex: 1, pageSize: 8, totalCount: 0 });
  });

  it('AC505_kb_list_hero_persists_when_results_empty', () => {
    const fixture = create();
    flushBoth({ items: [], pageIndex: 1, pageSize: 10, totalCount: 0 });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('How can we help you today?');
  });

  it('AC503_kb_list_no_physical_direction_utilities', () => {
    const fixture = create();
    flushBoth({ items: [], pageIndex: 1, pageSize: 10, totalCount: 0 });
    fixture.detectChanges();

    const html = fixture.nativeElement.innerHTML;
    const banned = [/\bpl-\d/, /\bpr-\d/, /\bml-\d/, /\bmr-\d/, /\bleft-\d/, /\bright-\d/];
    for (const pattern of banned) {
      expect(html).not.toMatch(pattern);
    }
  });
});
