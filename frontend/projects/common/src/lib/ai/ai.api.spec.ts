import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AiApi } from './ai.api';
import { envelopeInterceptor } from '../api/envelope.interceptor';

describe('AiApi', () => {
  let http: HttpTestingController;
  let api: AiApi;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(AiApi);
  });

  afterEach(() => http.verify());

  it('posts the summary command and unwraps the envelope to the suggestion', () => {
    let suggestion: object | undefined;
    api.summarise('t1').subscribe((s) => (suggestion = s));

    const req = http.expectOne('/api/Tickets/t1/ai/summary');
    expect(req.request.method).toBe('POST');
    req.flush({
      id: 's1',
      kind: 'Summary',
      payload: { text: 'hello' },
      status: 'Pending',
      edited: false,
    });

    expect(suggestion).toEqual({
      id: 's1',
      kind: 'Summary',
      payload: { text: 'hello' },
      status: 'Pending',
      edited: false,
    });
  });

  it('resolves a suggestion with accept and an edited payload', () => {
    api.resolve('t1', 's1', 'accept', '{"text":"edited"}').subscribe();

    const req = http.expectOne('/api/Tickets/t1/ai/suggestions/s1');
    expect(req.request.body).toEqual({ action: 'accept', editedPayload: '{"text":"edited"}' });
    req.flush({ id: 's1', kind: 'Summary', payload: {}, status: 'Accepted', edited: true });
  });

  it('asks the knowledge base on the external route with the question body', () => {
    let answer: object | undefined;
    api.ask('how do I reset?').subscribe((a) => (answer = a));

    const req = http.expectOne('/api/knowledge-base/ask');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ question: 'how do I reset?' });
    req.flush({ answer: 'Do this.', citations: [{ articleId: 'a1', title: 'Reset' }] });

    expect(answer).toEqual({ answer: 'Do this.', citations: [{ articleId: 'a1', title: 'Reset' }] });
  });
});
