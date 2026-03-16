export type DocumentFormat = 'Pdf' | 'Doc' | 'Docx';

export interface DocumentDto {
  id: string;
  originalFileName: string;
  contentType: string;
  fileSize: number;
  format: DocumentFormat;
  uploadedAt: string;
}
