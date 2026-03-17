import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AutoPrintResult } from '../models/auto-print.models';

@Injectable({ providedIn: 'root' })
export class AutoPrintService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/autoprint`;

  print(documentId: string) {
    return this.http.post<AutoPrintResult>(`${this.baseUrl}/${documentId}`, null);
  }
}
