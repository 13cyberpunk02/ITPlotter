export type PrinterType = 'Printer' | 'Plotter';

export type PrinterStatus =
  | 'Idle'
  | 'Printing'
  | 'PaperJam'
  | 'OutOfPaper'
  | 'OutOfToner'
  | 'OutOfInk'
  | 'Error'
  | 'Offline';

export type PaperFormat = 'A4' | 'A3' | 'A2' | 'A1' | 'A0';

export interface PrinterDto {
  id: string;
  name: string;
  cupsName: string;
  location: string;
  type: PrinterType;
  status: PrinterStatus;
  maxPaperFormat: PaperFormat;
  tonerLevelPercent: number | null;
  inkLevelPercent: number | null;
  paperRemaining: number | null;
}

export interface CreatePrinterRequest {
  name: string;
  cupsName: string;
  deviceUri: string;
  driverUri: string;
  location: string;
  type: PrinterType;
  maxPaperFormat: PaperFormat;
}

export interface UpdatePrinterRequest {
  name?: string;
  location?: string;
  type?: PrinterType;
  maxPaperFormat?: PaperFormat;
}
