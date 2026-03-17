import { PaperFormat } from './printer.models';

export interface AutoPrintResult {
  documentName: string;
  totalPages: number;
  jobsCreated: number;
  unmatchedPages: number;
  jobs: AutoPrintJobInfo[];
}

export interface AutoPrintJobInfo {
  jobId: string;
  printerName: string;
  paperFormat: PaperFormat;
}
