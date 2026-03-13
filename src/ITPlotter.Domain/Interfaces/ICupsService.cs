using ITPlotter.Domain.Entities;
using ITPlotter.Domain.Enums;

namespace ITPlotter.Domain.Interfaces;

public interface ICupsService
{
    Task<IReadOnlyList<CupsPrinterInfo>> GetPrintersAsync(CancellationToken ct = default);
    Task<CupsPrinterInfo?> GetPrinterAsync(string printerName, CancellationToken ct = default);
    Task AddPrinterAsync(string printerName, string deviceUri, string driverUri, CancellationToken ct = default);
    Task RemovePrinterAsync(string printerName, CancellationToken ct = default);
    Task<int> PrintFileAsync(string printerName, Stream fileStream, string fileName, PrintJobOptions options, CancellationToken ct = default);
    Task<CupsJobInfo?> GetJobStatusAsync(int jobId, CancellationToken ct = default);
    Task CancelJobAsync(int jobId, CancellationToken ct = default);
}

public class CupsPrinterInfo
{
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string StateMessage { get; set; } = string.Empty;
    public int? TonerLevel { get; set; }
    public int? InkLevel { get; set; }
    public int? PaperRemaining { get; set; }
    public bool IsAcceptingJobs { get; set; }
}

public class CupsJobInfo
{
    public int JobId { get; set; }
    public string State { get; set; } = string.Empty;
    public string PrinterName { get; set; } = string.Empty;
}

public class PrintJobOptions
{
    public int Copies { get; set; } = 1;
    public PaperFormat PaperFormat { get; set; } = PaperFormat.A4;
}
