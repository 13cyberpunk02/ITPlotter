import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class OptimizationService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/optimization`;

  optimize(documentId: string) {
    return this.http.post<unknown>(`${this.baseUrl}/${documentId}`, null);
  }
}
