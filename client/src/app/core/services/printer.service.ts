import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import {
  PrinterDto,
  CreatePrinterRequest,
  UpdatePrinterRequest,
} from '../models/printer.models';

@Injectable({ providedIn: 'root' })
export class PrinterService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/printers`;

  getAll() {
    return this.http.get<PrinterDto[]>(this.baseUrl);
  }

  getById(id: string) {
    return this.http.get<PrinterDto>(`${this.baseUrl}/${id}`);
  }

  create(request: CreatePrinterRequest) {
    return this.http.post<PrinterDto>(this.baseUrl, request);
  }

  update(id: string, request: UpdatePrinterRequest) {
    return this.http.put<PrinterDto>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: string) {
    return this.http.delete(`${this.baseUrl}/${id}`);
  }

  syncStatus(id: string) {
    return this.http.post(`${this.baseUrl}/${id}/sync-status`, null);
  }
}
