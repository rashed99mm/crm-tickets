import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface CmsErpImportResult {
  readonly imported: number;
  readonly skipped: number;
  readonly ticketReferences: readonly string[];
}

@Injectable({ providedIn: 'root' })
export class CmsIntegrationApi {
  private readonly http = inject(HttpClient);

  importErpTickets(): Observable<CmsErpImportResult> {
    return this.http.post<CmsErpImportResult>('/api/integrations/cms/erp/import-tickets', {});
  }
}
