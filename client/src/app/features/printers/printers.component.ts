import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PrinterService } from '../../core/services/printer.service';
import { ToastService } from '../../core/services/toast.service';
import { ConfirmDialogComponent } from '../../shared/components/confirm-dialog/confirm-dialog.component';
import {
  PrinterDto,
  CreatePrinterRequest,
  PrinterType,
  PaperFormat,
} from '../../core/models/printer.models';

@Component({
  selector: 'app-printers',
  imports: [CommonModule, FormsModule, ConfirmDialogComponent],
  templateUrl: './printers.component.html',
  styleUrl: './printers.component.css',
})
export class PrintersComponent implements OnInit {
  private readonly printerService = inject(PrinterService);
  private readonly toast = inject(ToastService);

  printers = signal<PrinterDto[]>([]);
  loading = signal(true);
  showAddForm = signal(false);
  deleteTarget = signal<PrinterDto | null>(null);
  syncing = signal<string | null>(null);

  newPrinter: CreatePrinterRequest = {
    name: '',
    cupsName: '',
    deviceUri: '',
    driverUri: '',
    location: '',
    type: 'Printer',
    maxPaperFormat: 'A4',
  };

  readonly printerTypes: PrinterType[] = ['Printer', 'Plotter'];
  readonly paperFormats: PaperFormat[] = ['A4', 'A3', 'A2', 'A1', 'A0'];

  ngOnInit(): void {
    this.loadPrinters();
  }

  loadPrinters(): void {
    this.printerService.getAll().subscribe({
      next: (printers) => {
        this.printers.set(printers);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  addPrinter(): void {
    if (!this.newPrinter.name || !this.newPrinter.cupsName) return;
    this.printerService.create(this.newPrinter).subscribe({
      next: (printer) => {
        this.printers.update(list => [...list, printer]);
        this.showAddForm.set(false);
        this.resetForm();
        this.toast.success('Printer added successfully');
      },
      error: () => this.toast.error('Failed to add printer'),
    });
  }

  syncStatus(printer: PrinterDto): void {
    this.syncing.set(printer.id);
    this.printerService.syncStatus(printer.id).subscribe({
      next: () => {
        this.printerService.getById(printer.id).subscribe({
          next: (updated) => {
            this.printers.update(list =>
              list.map(p => (p.id === updated.id ? updated : p))
            );
            this.syncing.set(null);
            this.toast.success('Status synced');
          },
        });
      },
      error: () => {
        this.syncing.set(null);
        this.toast.error('Failed to sync status');
      },
    });
  }

  confirmDelete(printer: PrinterDto): void {
    this.deleteTarget.set(printer);
  }

  deletePrinter(): void {
    const target = this.deleteTarget();
    if (!target) return;
    this.printerService.delete(target.id).subscribe({
      next: () => {
        this.printers.update(list => list.filter(p => p.id !== target.id));
        this.deleteTarget.set(null);
        this.toast.success('Printer deleted');
      },
      error: () => {
        this.deleteTarget.set(null);
        this.toast.error('Failed to delete printer');
      },
    });
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Idle': return 'status-idle';
      case 'Printing': return 'status-printing';
      case 'Offline': return 'status-offline';
      default: return 'status-warning';
    }
  }

  private resetForm(): void {
    this.newPrinter = {
      name: '',
      cupsName: '',
      deviceUri: '',
      driverUri: '',
      location: '',
      type: 'Printer',
      maxPaperFormat: 'A4',
    };
  }
}
