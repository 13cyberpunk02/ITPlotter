using ITPlotter.Domain.Enums;

namespace ITPlotter.Application.DTOs.Printers;

public record UpdatePrinterRequest(
    string? Name,
    string? Location,
    PrinterType? Type,
    PaperFormat? MaxPaperFormat);
