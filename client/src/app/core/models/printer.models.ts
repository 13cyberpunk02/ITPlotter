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

export type PaperFormat =
  | 'A4' | 'A3' | 'A2' | 'A1' | 'A0'
  | 'A4x3' | 'A4x4' | 'A4x5' | 'A4x6' | 'A4x7' | 'A4x8' | 'A4x9'
  | 'A3x3' | 'A3x4' | 'A3x5' | 'A3x6' | 'A3x7'
  | 'A2x3' | 'A2x4' | 'A2x5'
  | 'A1x3' | 'A1x4'
  | 'A0x2' | 'A0x3';

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
