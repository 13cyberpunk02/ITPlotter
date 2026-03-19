import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { PrintJobService } from '../../core/services/print-job.service';
import { PrinterService } from '../../core/services/printer.service';
import { DocumentService } from '../../core/services/document.service';
import { ToastService } from '../../core/services/toast.service';
import { ConfirmDialogComponent } from '../../shared/components/confirm-dialog/confirm-dialog.component';
import { PrintJobDto, CreatePrintJobRequest } from '../../core/models/print-job.models';
import { PrinterDto, PaperFormat } from '../../core/models/printer.models';
import { DocumentDto } from '../../core/models/document.models';

@Component({
  selector: 'app-print-jobs',
  imports: [CommonModule, FormsModule, ConfirmDialogComponent],
  templateUrl: './print-jobs.component.html',
  styleUrl: './print-jobs.component.css',
})
export class PrintJobsComponent implements OnInit {
  private readonly printJobService = inject(PrintJobService);
  private readonly printerService = inject(PrinterService);
  private readonly documentService = inject(DocumentService);
  private readonly toast = inject(ToastService);

  printJobs = signal<PrintJobDto[]>([]);
  printers = signal<PrinterDto[]>([]);
  documents = signal<DocumentDto[]>([]);
  loading = signal(true);
  showCreateForm = signal(false);
  cancelTarget = signal<PrintJobDto | null>(null);

  selectedDocumentId = '';
  selectedPrinterId = '';
  copies = 1;
  paperFormat: PaperFormat = 'A4';

  readonly paperFormats: PaperFormat[] = ['A4', 'A3', 'A2', 'A1', 'A0'];

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    forkJoin({
      jobs: this.printJobService.getAll(),
      printers: this.printerService.getAll(),
      documents: this.documentService.getAll(),
    }).subscribe({
      next: (result) => {
        this.printJobs.set(result.jobs);
        this.printers.set(result.printers);
        this.documents.set(result.documents);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  createJob(): void {
    if (!this.selectedDocumentId || !this.selectedPrinterId) return;

    const request: CreatePrintJobRequest = {
      documentId: this.selectedDocumentId,
      printerId: this.selectedPrinterId,
      copies: this.copies,
      paperFormat: this.paperFormat,
    };

    this.printJobService.create(request).subscribe({
      next: (job) => {
        this.printJobs.update(list => [job, ...list]);
        this.showCreateForm.set(false);
        this.resetForm();
        this.toast.success('Задание создано');
      },
      error: () => this.toast.error('Не удалось создать задание'),
    });
  }

  refreshJob(job: PrintJobDto): void {
    this.printJobService.getById(job.id).subscribe({
      next: (updated) => {
        this.printJobs.update(list =>
          list.map(j => (j.id === updated.id ? updated : j))
        );
      },
    });
  }

  confirmCancel(job: PrintJobDto): void {
    this.cancelTarget.set(job);
  }

  cancelJob(): void {
    const target = this.cancelTarget();
    if (!target) return;
    this.printJobService.cancel(target.id).subscribe({
      next: () => {
        this.printJobs.update(list =>
          list.map(j => (j.id === target.id ? { ...j, status: 'Cancelled' as const } : j))
        );
        this.cancelTarget.set(null);
        this.toast.success('Задание отменено');
      },
      error: () => {
        this.cancelTarget.set(null);
        this.toast.error('Не удалось отменить задание');
      },
    });
  }

  canCancel(job: PrintJobDto): boolean {
    return job.status === 'Pending' || job.status === 'Processing' || job.status === 'Printing';
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Completed': return 'status-completed';
      case 'Printing':
      case 'Processing': return 'status-printing';
      case 'Pending': return 'status-pending';
      case 'Failed': return 'status-failed';
      case 'Cancelled': return 'status-cancelled';
      default: return '';
    }
  }

  private resetForm(): void {
    this.selectedDocumentId = '';
    this.selectedPrinterId = '';
    this.copies = 1;
    this.paperFormat = 'A4';
  }
}
