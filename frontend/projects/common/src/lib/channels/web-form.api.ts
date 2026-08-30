import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface WebFormSubmissionRequest {
  readonly name: string;
  readonly email: string;
  readonly subject: string;
  readonly description: string;
  readonly honeypot?: string;
}

export interface WebFormSubmissionResponse {
  readonly reference: string;
  readonly success: boolean;
}

@Injectable({ providedIn: 'root' })
export class WebFormApi {
  private readonly http = inject(HttpClient);

  submit(request: WebFormSubmissionRequest): Observable<WebFormSubmissionResponse> {
    return this.http.post<WebFormSubmissionResponse>('/api/external/webform/submit', request);
  }
}
