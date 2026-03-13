using ITPlotter.Domain.Enums;

namespace ITPlotter.Application.DTOs.PrintJobs;

public record PrintJobDto(
    Guid Id,
    int CupsJobId,
    PrintJobStatus Status,
    int Copies,
    PaperFormat PaperFormat,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? ErrorMessage,
    string DocumentName,
    string PrinterName);
