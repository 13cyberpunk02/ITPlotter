import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpEventType } from '@angular/common/http';
import { DocumentService } from '../../core/services/document.service';
import { OptimizationService } from '../../core/services/optimization.service';
import { ToastService } from '../../core/services/toast.service';
import { ConfirmDialogComponent } from '../../shared/components/confirm-dialog/confirm-dialog.component';
import { FileSizePipe } from '../../shared/pipes/file-size.pipe';
import { DocumentDto } from '../../core/models/document.models';

@Component({
  selector: 'app-documents',
  imports: [CommonModule, ConfirmDialogComponent, FileSizePipe],
  templateUrl: './documents.component.html',
  styleUrl: './documents.component.css',
})
export class DocumentsComponent implements OnInit {
  private readonly documentService = inject(DocumentService);
  private readonly optimizationService = inject(OptimizationService);
  private readonly toast = inject(ToastService);

  documents = signal<DocumentDto[]>([]);
  loading = signal(true);
  uploading = signal(false);
  uploadProgress = signal(0);
  deleteTarget = signal<DocumentDto | null>(null);
  optimizing = signal<string | null>(null);

  ngOnInit(): void {
    this.loadDocuments();
  }

  loadDocuments(): void {
    this.documentService.getAll().subscribe({
      next: (docs) => {
        this.documents.set(docs);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.uploading.set(true);
    this.uploadProgress.set(0);

    this.documentService.upload(file).subscribe({
      next: (event) => {
        if (event.type === HttpEventType.UploadProgress && event.total) {
          this.uploadProgress.set(Math.round((100 * event.loaded) / event.total));
        } else if (event.type === HttpEventType.Response) {
          const doc = event.body;
          if (doc) {
            this.documents.update(list => [doc, ...list]);
          }
          this.uploading.set(false);
          this.toast.success('Document uploaded');
        }
      },
      error: () => {
        this.uploading.set(false);
        this.toast.error('Upload failed');
      },
    });
    input.value = '';
  }

  download(doc: DocumentDto): void {
    this.documentService.download(doc.id).subscribe({
      next: (response) => {
        const blob = response.body;
        if (!blob) return;
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = doc.originalFileName;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.toast.error('Download failed'),
    });
  }

  optimize(doc: DocumentDto): void {
    if (doc.format !== 'Pdf') {
      this.toast.error('Only PDF documents can be optimized');
      return;
    }
    this.optimizing.set(doc.id);
    this.optimizationService.optimize(doc.id).subscribe({
      next: () => {
        this.optimizing.set(null);
        this.toast.success('Document optimized for plotting');
      },
      error: () => {
        this.optimizing.set(null);
        this.toast.error('Optimization failed');
      },
    });
  }

  confirmDelete(doc: DocumentDto): void {
    this.deleteTarget.set(doc);
  }

  deleteDocument(): void {
    const target = this.deleteTarget();
    if (!target) return;
    this.documentService.delete(target.id).subscribe({
      next: () => {
        this.documents.update(list => list.filter(d => d.id !== target.id));
        this.deleteTarget.set(null);
        this.toast.success('Document deleted');
      },
      error: () => {
        this.deleteTarget.set(null);
        this.toast.error('Failed to delete document');
      },
    });
  }

  getFormatClass(format: string): string {
    switch (format) {
      case 'Pdf': return 'format-pdf';
      case 'Doc':
      case 'Docx': return 'format-doc';
      default: return '';
    }
  }
}
