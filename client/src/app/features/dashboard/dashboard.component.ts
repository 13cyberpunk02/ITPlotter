import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { PrinterService } from '../../core/services/printer.service';
import { DocumentService } from '../../core/services/document.service';
import { PrintJobService } from '../../core/services/print-job.service';
import { PrinterDto } from '../../core/models/printer.models';
import { DocumentDto } from '../../core/models/document.models';
import { PrintJobDto } from '../../core/models/print-job.models';

@Component({
  selector: 'app-dashboard',
  imports: [CommonModule, RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css',
})
export class DashboardComponent implements OnInit {
  private readonly printerService = inject(PrinterService);
  private readonly documentService = inject(DocumentService);
  private readonly printJobService = inject(PrintJobService);

  printers = signal<PrinterDto[]>([]);
  documents = signal<DocumentDto[]>([]);
  printJobs = signal<PrintJobDto[]>([]);
  loading = signal(true);

  ngOnInit(): void {
    forkJoin({
      printers: this.printerService.getAll(),
      documents: this.documentService.getAll(),
      printJobs: this.printJobService.getAll(),
    }).subscribe({
      next: (result) => {
        this.printers.set(result.printers);
        this.documents.set(result.documents);
        this.printJobs.set(result.printJobs);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  get onlinePrinters(): number {
    return this.printers().filter(p => p.status !== 'Offline' && p.status !== 'Error').length;
  }

  get activePrintJobs(): number {
    return this.printJobs().filter(j => j.status === 'Pending' || j.status === 'Processing' || j.status === 'Printing').length;
  }

  get recentJobs(): PrintJobDto[] {
    return this.printJobs().slice(0, 5);
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Idle': return 'status-idle';
      case 'Printing': return 'status-printing';
      case 'Completed': return 'status-completed';
      case 'Failed':
      case 'Error':
      case 'Offline': return 'status-error';
      default: return 'status-warning';
    }
  }
}
