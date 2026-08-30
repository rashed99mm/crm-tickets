import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

/** Matches the backend's `ReportBucket` (`Key`, `Count`) — AC-149/150/151. */
export interface ReportBucket {
  readonly key: string;
  readonly count: number;
}

/** Matches the backend's `TicketVolumeReportDto`. */
export interface TicketVolumeReport {
  readonly byPeriod: readonly ReportBucket[];
  readonly byCategory: readonly ReportBucket[];
  readonly byPriority: readonly ReportBucket[];
}

/** Matches the backend's `SlaPerformanceRow`/`SlaPerformanceReportDto` — AC-152. */
export interface SlaPerformanceRow {
  readonly priority: string;
  readonly total: number;
  readonly metFirstResponse: number;
  readonly breachedFirstResponse: number;
  readonly metResolution: number;
  readonly breachedResolution: number;
}

export interface SlaPerformanceReport {
  readonly byPriority: readonly SlaPerformanceRow[];
}

/** Matches the backend's `AgentPerformanceRow`/`AgentPerformanceReportDto` — AC-153. */
export interface AgentPerformanceRow {
  readonly agentId: string;
  readonly agentName: string;
  readonly ticketsResolved: number;
  readonly avgHandleMinutes: number;
}

export interface AgentPerformanceReport {
  readonly byAgent: readonly AgentPerformanceRow[];
}

/** Matches the backend's `CsatReportDto` (US-605) — average rating and the NMS-style split. */
export interface CsatReport {
  readonly totalResponses: number;
  readonly averageRating: number;
  readonly promoters: number;
  readonly passives: number;
  readonly detractors: number;
  readonly byRating: readonly { rating: number; count: number }[];
}

export interface ReportDateRange {
  readonly from: string;
  readonly to: string;
}

export type ReportGroupBy = 'day' | 'week' | 'month';

/**
 * The report clients FEAT-20 ships. CSAT (US-605) was reopened by sprint storydept once the
 * portal survey had a data source. Catches nothing, matching every other API service here.
 */
@Injectable({ providedIn: 'root' })
export class ReportsApi {
  private readonly http = inject(HttpClient);

  ticketVolume(range: ReportDateRange, groupBy: ReportGroupBy = 'day'): Observable<TicketVolumeReport> {
    const params = new HttpParams()
      .set('from', range.from)
      .set('to', range.to)
      .set('groupBy', groupBy);
    return this.http.get<TicketVolumeReport>('/api/reports/ticket-volume', { params });
  }

  slaPerformance(range: ReportDateRange): Observable<SlaPerformanceReport> {
    const params = new HttpParams().set('from', range.from).set('to', range.to);
    return this.http.get<SlaPerformanceReport>('/api/reports/sla-performance', { params });
  }

  agentPerformance(range: ReportDateRange): Observable<AgentPerformanceReport> {
    const params = new HttpParams().set('from', range.from).set('to', range.to);
    return this.http.get<AgentPerformanceReport>('/api/reports/agent-performance', { params });
  }

  csat(range: ReportDateRange): Observable<CsatReport> {
    const params = new HttpParams().set('from', range.from).set('to', range.to);
    return this.http.get<CsatReport>('/api/reports/csat', { params });
  }
}
