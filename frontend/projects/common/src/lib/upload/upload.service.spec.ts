import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { UploadService } from './upload.service';

describe('UploadService', () => {
  let service: UploadService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(UploadService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('upload: sends multipart/form-data to specified endpoint', () => {
    const file = new File(['dummy-content'], 'avatar.png', { type: 'image/png' });
    let result: unknown;
    service.upload(file, '/api/upload').subscribe((res) => (result = res));

    const req = http.expectOne('/api/upload');
    expect(req.request.method).toBe('POST');
    expect(req.request.body instanceof FormData).toBe(true);
    req.flush({ url: 'https://cdn.local/avatar.png' });

    expect(result).toEqual({ url: 'https://cdn.local/avatar.png' });
  });

  it('validateImage: returns invalid when file is not an image', () => {
    const textFile = new File(['text'], 'doc.txt', { type: 'text/plain' });
    const check = service.validateImage(textFile);
    expect(check.valid).toBe(false);
  });

  it('validateImage: returns valid for valid image under size limit', () => {
    const imgFile = new File(['img'], 'photo.jpg', { type: 'image/jpeg' });
    const check = service.validateImage(imgFile);
    expect(check.valid).toBe(true);
  });
});
