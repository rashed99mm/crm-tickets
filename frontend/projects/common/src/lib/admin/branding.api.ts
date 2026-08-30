import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiEnvelope } from '../api/api-response';

export interface BrandingDto {
  readonly logoUrl: string;
  readonly primaryColor: string;
  readonly accentColor: string;
}

@Injectable({ providedIn: 'root' })
export class BrandingApi {
  private readonly http = inject(HttpClient);

  get(): Observable<ApiEnvelope<BrandingDto>> {
    return this.http.get<ApiEnvelope<BrandingDto>>('/api/PlatformSettings/branding');
  }

  update(request: BrandingDto): Observable<ApiEnvelope<BrandingDto>> {
    return this.http.put<ApiEnvelope<BrandingDto>>('/api/PlatformSettings/branding', request);
  }
}
