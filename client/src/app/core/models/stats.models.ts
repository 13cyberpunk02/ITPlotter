import { PaperFormat } from './printer.models';

export interface PrintStatsDto {
  totalJobs: number;
  totalPages: number;
  byFormat: FormatStatsDto[];
  recent: DailyStatsDto[];
}

export interface FormatStatsDto {
  format: PaperFormat;
  pages: number;
}

export interface DailyStatsDto {
  date: string;
  pages: number;
}
