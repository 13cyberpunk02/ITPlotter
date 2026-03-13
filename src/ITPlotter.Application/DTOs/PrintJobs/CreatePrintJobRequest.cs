using ITPlotter.Domain.Enums;

namespace ITPlotter.Application.DTOs.PrintJobs;

public record CreatePrintJobRequest(
    Guid DocumentId,
    Guid PrinterId,
    int Copies = 1,
    PaperFormat PaperFormat = PaperFormat.A4);
