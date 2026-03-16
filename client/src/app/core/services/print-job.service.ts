import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import {
  PrintJobDto,
  CreatePrintJobRequest,
} from '../models/print-job.models';

@Injectable({ providedIn: 'root' })
export class PrintJobService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/printjobs`;

  getAll() {
    return this.http.get<PrintJobDto[]>(this.baseUrl);
  }

  getById(id: string) {
    return this.http.get<PrintJobDto>(`${this.baseUrl}/${id}`);
  }

  create(request: CreatePrintJobRequest) {
    return this.http.post<PrintJobDto>(this.baseUrl, request);
  }

  cancel(id: string) {
    return this.http.delete(`${this.baseUrl}/${id}`);
  }
}
