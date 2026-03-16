import { PaperFormat } from './printer.models';

export type PrintJobStatus =
  | 'Pending'
  | 'Processing'
  | 'Printing'
  | 'Completed'
  | 'Failed'
  | 'Cancelled';

export interface CreatePrintJobRequest {
  documentId: string;
  printerId: string;
  copies: number;
  paperFormat: PaperFormat;
}

export interface PrintJobDto {
  id: string;
  cupsJobId: number;
  status: PrintJobStatus;
  copies: number;
  paperFormat: PaperFormat;
  createdAt: string;
  completedAt: string | null;
  errorMessage: string | null;
  documentName: string;
  printerName: string;
}
