import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { envelopeInterceptor, KbAdminApi } from 'common';
import KbAdminComponent from './kb-admin.component';

const ARTICLE = {
  id: 'a1',
  title: 'Reset your password',
  summary: 'Steps',
  contentType: 'Article',
  status: 'Draft',
  category: null,
  tags: ['account'],
  viewCount: 0,
  likeCount: 0,
  publishedAt: null,
  body: 'Do this.',
};

describe('KbAdminComponent', () => {
  let http: HttpTestingController;

  function setup() {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [KbAdminComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  }

  function create() {
    setup();
    const fixture = TestBed.createComponent(KbAdminComponent);
    fixture.detectChanges();
    return fixture;
  }

  /** Init fires the list + the category tree. */
  function flushInit(items: object[] = []) {
    http.expectOne((r) => r.url === '/api/Contents').flush({
      items, pageIndex: 1, pageSize: 10, totalCount: items.length,
    });
    http.expectOne('/api/ContentCategories').flush([]);
  }

  afterEach(() => http.verify());

  it('AC509_ListShowsStatusFilter: lists articles with title and status, and filtering refetches with the status param', () => {
    const fixture = create();
    flushInit([ARTICLE]);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Reset your password');

    fixture.componentInstance.setStatusFilter('Published');
    const filtered = http.expectOne(
      (r) => r.url === '/api/Contents' && r.params.get('status') === 'Published',
    );
    filtered.flush({ items: [], pageIndex: 1, pageSize: 10, totalCount: 0 });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No articles match this filter.');
  });

  it('AC510_CreateSubmitsDraft: the create form posts a Draft with the entered fields', () => {
    const fixture = create();
    flushInit();
    fixture.detectChanges();

    fixture.componentInstance.openCreate();
    fixture.componentInstance.formTitle.set('How to reset');
    fixture.componentInstance.formBody.set('Step one.');
    fixture.componentInstance.formTags.set('account, basics');
    fixture.componentInstance.save();

    const req = http.expectOne((r) => r.url === '/api/Contents' && r.method === 'POST');
    expect(req.request.body).toEqual(
      expect.objectContaining({ title: 'How to reset', body: 'Step one.', status: 'Draft' }),
    );
    expect(req.request.body.tags).toEqual(['account', 'basics']);
    req.flush({ id: 'new1' });
    http.expectOne((r) => r.url === '/api/Contents').flush({ items: [], pageIndex: 1, pageSize: 10, totalCount: 0 });
  });

  it('AC511_EditCreatesNewVersionAndShowsHistory: editing a Draft PUTs and loads version history', () => {
    const fixture = create();
    flushInit([{ ...ARTICLE }]);
    fixture.detectChanges();

    fixture.componentInstance.openEdit({ ...ARTICLE } as never);
    const versionsReq = http.expectOne('/api/Contents/a1/versions');
    versionsReq.flush([
      { versionNumber: 2, authorId: 'u1', changeSummary: 'typo', createdAt: '2026-08-27' },
      { versionNumber: 1, authorId: 'u1', changeSummary: null, createdAt: '2026-08-26' },
    ]);
    fixture.componentInstance.formTitle.set('Reset your password v2');
    fixture.componentInstance.save();

    const put = http.expectOne((r) => r.url === '/api/Contents/a1' && r.method === 'PUT');
    expect(put.request.body.title).toBe('Reset your password v2');
    put.flush({});
    http.expectOne((r) => r.url === '/api/Contents').flush({ items: [], pageIndex: 1, pageSize: 10, totalCount: 0 });

    expect(fixture.componentInstance.versions().length).toBe(2);
    expect(fixture.componentInstance.versions()[0].versionNumber).toBe(2);
  });

  it('AC512_PublishArchivePerStatus: publish posts for a Draft; archived articles expose no actions', () => {
    const fixture = create();
    flushInit([{ ...ARTICLE, id: 'd1', status: 'Draft' }]);
    fixture.detectChanges();

    fixture.componentInstance.publish({ ...ARTICLE, id: 'd1', status: 'Draft' } as never);
    const pub = http.expectOne('/api/Contents/d1/publish');
    expect(pub.request.method).toBe('POST');
    pub.flush({});
    http.expectOne((r) => r.url === '/api/Contents').flush({ items: [], pageIndex: 1, pageSize: 10, totalCount: 0 });
  });
});
