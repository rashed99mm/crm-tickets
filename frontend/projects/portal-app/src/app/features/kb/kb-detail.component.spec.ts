import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { envelopeInterceptor, ContentsApi, SessionStore } from 'common';
import PortalKbDetailComponent from './kb-detail.component';

const ARTICLE = {
  id: 'a1',
  title: 'How to reset your password',
  summary: 'Steps to reset',
  contentType: 'Article',
  status: 'Published',
  category: 'Account',
  categoryName: 'Account',
  tags: ['password', 'security'],
  viewCount: 12,
  likeCount: 3,
  publishedAt: null,
  body: 'Do this.',
};

describe('PortalKbDetailComponent', () => {
  let http: HttpTestingController;

  function setup(authed = false) {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [PortalKbDetailComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    if (authed) {
      TestBed.inject(SessionStore).signIn({
        userId: 'u1',
        email: 'a@b.com',
        firstName: 'A',
        lastName: 'B',
        accessToken: `a.${btoa('{}')}.c`,
        refreshToken: 'rt',
        accessTokenExpiresAt: '',
        refreshTokenExpiresAt: '',
        roles: [],
      });
    }
  }

  function create(authed = false) {
    setup(authed);
    const fixture = TestBed.createComponent(PortalKbDetailComponent);
    fixture.componentRef.setInput('id', 'a1');
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => http.verify());

  it('loads an article from the public knowledge-base endpoint (defect fix)', () => {
    const fixture = create();
    const req = http.expectOne('/api/knowledge-base/articles/a1');
    expect(req.request.method).toBe('GET');
    req.flush(ARTICLE);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('How to reset your password');
    expect(el.textContent).toContain('password');
    expect(el.textContent).toContain('12 views');
  });

  it('shows the helpfulness vote controls only when authenticated (US-508/513)', () => {
    const anon = create(false);
    http.expectOne('/api/knowledge-base/articles/a1').flush(ARTICLE);
    anon.detectChanges();
    expect(anon.nativeElement.querySelector('button')).toBeNull();

    const authed = create(true);
    http.expectOne('/api/knowledge-base/articles/a1').flush(ARTICLE);
    authed.detectChanges();
    expect(authed.nativeElement.querySelector('button')).not.toBeNull();
  });

  it('posts a helpfulness vote and locks the control afterward', () => {
    const fixture = create(true);
    http.expectOne('/api/knowledge-base/articles/a1').flush(ARTICLE);
    fixture.detectChanges();

    const buttons = fixture.nativeElement.querySelectorAll('button');
    buttons[0].dispatchEvent(new Event('click'));
    const voteReq = http.expectOne('/api/knowledge-base/articles/a1/vote');
    expect(voteReq.request.method).toBe('POST');
    expect(voteReq.request.body).toEqual({ isHelpful: true });
    voteReq.flush({});
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Thanks for the feedback!');
  });
});
