import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { PrintStatsDto } from '../models/stats.models';

@Injectable({ providedIn: 'root' })
export class StatsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/stats`;

  getMyStats() {
    return this.http.get<PrintStatsDto>(this.baseUrl);
  }
}
