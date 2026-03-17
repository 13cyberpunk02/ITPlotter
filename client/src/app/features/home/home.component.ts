import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpEventType } from '@angular/common/http';
import { DocumentService } from '../../core/services/document.service';
import { AutoPrintService } from '../../core/services/auto-print.service';
import { ToastService } from '../../core/services/toast.service';
import { AutoPrintResult } from '../../core/models/auto-print.models';

type UploadState = 'idle' | 'uploading' | 'processing' | 'done' | 'error';

@Component({
  selector: 'app-home',
  imports: [CommonModule],
  templateUrl: './home.component.html',
  styleUrl: './home.component.css',
})
export class HomeComponent {
  private readonly documentService = inject(DocumentService);
  private readonly autoPrintService = inject(AutoPrintService);
  private readonly toast = inject(ToastService);

  state = signal<UploadState>('idle');
  uploadProgress = signal(0);
  statusMessage = signal('');
  lastResult = signal<AutoPrintResult | null>(null);
  dragOver = signal(false);

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.dragOver.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.dragOver.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.dragOver.set(false);

    const file = event.dataTransfer?.files?.[0];
    if (file) this.processFile(file);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) this.processFile(file);
    input.value = '';
  }

  private processFile(file: File): void {
    if (!file.name.toLowerCase().endsWith('.pdf')) {
      this.toast.error('Поддерживаются только PDF файлы');
      return;
    }

    this.state.set('uploading');
    this.uploadProgress.set(0);
    this.statusMessage.set('Загрузка файла...');
    this.lastResult.set(null);

    this.documentService.upload(file).subscribe({
      next: (event) => {
        if (event.type === HttpEventType.UploadProgress && event.total) {
          this.uploadProgress.set(Math.round((100 * event.loaded) / event.total));
        } else if (event.type === HttpEventType.Response && event.body) {
          this.startAutoPrint(event.body.id, file.name);
        }
      },
      error: () => {
        this.state.set('error');
        this.statusMessage.set('Ошибка при загрузке файла');
        this.toast.error('Не удалось загрузить файл');
      },
    });
  }

  private startAutoPrint(documentId: string, fileName: string): void {
    this.state.set('processing');
    this.statusMessage.set('Анализ и оптимизация документа...');

    this.autoPrintService.print(documentId).subscribe({
      next: (result) => {
        this.lastResult.set(result);
        this.state.set('done');
        if (result.jobsCreated > 0) {
          this.statusMessage.set(`Отправлено на печать: ${result.jobsCreated} заданий`);
          this.toast.success(`${fileName} отправлен на печать`);
        } else {
          this.statusMessage.set('Не удалось найти подходящие принтеры');
          this.toast.error('Нет доступных принтеров для этого документа');
        }
      },
      error: (err) => {
        this.state.set('error');
        const msg = err.error?.message || 'Ошибка при обработке';
        this.statusMessage.set(msg);
        this.toast.error(msg);
      },
    });
  }

  reset(): void {
    this.state.set('idle');
    this.statusMessage.set('');
    this.lastResult.set(null);
    this.uploadProgress.set(0);
  }
}
