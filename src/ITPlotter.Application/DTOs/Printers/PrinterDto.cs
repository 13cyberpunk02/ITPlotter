using ITPlotter.Domain.Enums;

namespace ITPlotter.Application.DTOs.Printers;

public record PrinterDto(
    Guid Id,
    string Name,
    string CupsName,
    string Location,
    PrinterType Type,
    PrinterStatus Status,
    PaperFormat MaxPaperFormat,
    int? TonerLevelPercent,
    int? InkLevelPercent,
    int? PaperRemaining);
