import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApiError } from './api-error';
import { envelopeInterceptor } from './envelope.interceptor';

describe('envelopeInterceptor', () => {
  let http: HttpClient;
  let mock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    mock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => mock.verify());

  it('unwraps a success envelope to its data', async () => {
    const promise = new Promise((resolve) =>
      http.get<{ id: string }>('/api/customers/1').subscribe(resolve));

    mock.expectOne('/api/customers/1').flush({
      success: true,
      code: 'CON023',
      message: 'Customer loaded',
      data: { id: 'c-1' },
      errors: [],
    });

    expect(await promise).toEqual({ id: 'c-1' });
  });

  it('throws ApiError carrying code and message on failure', async () => {
    const caught = new Promise<ApiError>((resolve) =>
      http.get('/api/customers/9').subscribe({ error: resolve }));

    mock.expectOne('/api/customers/9').flush(
      {
        success: false,
        code: 'ERR007',
        message: 'Customer not found',
        data: null,
        errors: [],
        traceId: 'abc-123',
      },
      { status: 404, statusText: 'Not Found' },
    );

    const error = await caught;
    expect(error).toBeInstanceOf(ApiError);
    expect(error.code).toBe('ERR007');
    expect(error.message_).toBe('Customer not found');
    expect(error.status).toBe(404);
    expect(error.hasFieldErrors).toBe(false);
  });

  it('exposes validation field errors by camelCased field name', async () => {
    const caught = new Promise<ApiError>((resolve) =>
      http.post('/api/customers', {}).subscribe({ error: resolve }));

    mock.expectOne('/api/customers').flush(
      {
        success: false,
        code: 'VAL001',
        message: 'Validation error',
        data: null,
        errors: [
          { field: 'Email', code: 'VAL003', message: 'Invalid email format' },
          { field: 'Name', code: 'VAL012', message: 'Name is required' },
        ],
      },
      { status: 400, statusText: 'Bad Request' },
    );

    const error = await caught;
    expect(error.errors).toHaveLength(2);
    expect(error.fieldError('email')?.message).toBe('Invalid email format');
    expect(error.fieldError('name')?.message).toBe('Name is required');
    expect(error.fieldError('phone')).toBeUndefined();
  });

  it('passes through a non-envelope body unchanged', async () => {
    // The OpenAPI document and static assets are not enveloped.
    const promise = new Promise((resolve) =>
      http.get('/openapi/v1.json').subscribe(resolve));

    mock.expectOne('/openapi/v1.json').flush({ openapi: '3.1.0' });

    expect(await promise).toEqual({ openapi: '3.1.0' });
  });

  it('wraps a transport failure that carries no envelope', async () => {
    const caught = new Promise<ApiError>((resolve) =>
      http.get('/api/customers').subscribe({ error: resolve }));

    mock.expectOne('/api/customers').error(new ProgressEvent('error'), {
      status: 0,
      statusText: 'Unknown Error',
    });

    const error = await caught;
    expect(error).toBeInstanceOf(ApiError);
    // A network failure still has to arrive as something displayable, not raw.
    expect(error.message_).not.toBe('');
  });
});
