using ITPlotter.Domain.Enums;

namespace ITPlotter.Application.DTOs.Printers;

public record CreatePrinterRequest(
    string Name,
    string CupsName,
    string DeviceUri,
    string DriverUri,
    string Location,
    PrinterType Type,
    PaperFormat MaxPaperFormat);
