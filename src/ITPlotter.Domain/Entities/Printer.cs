using ITPlotter.Domain.Enums;

namespace ITPlotter.Domain.Entities;

public class Printer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CupsName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public PrinterType Type { get; set; }
    public PrinterStatus Status { get; set; } = PrinterStatus.Idle;
    public PaperFormat MaxPaperFormat { get; set; }

    public int? TonerLevelPercent { get; set; }
    public int? InkLevelPercent { get; set; }
    public int? PaperRemaining { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastStatusUpdate { get; set; }

    public ICollection<PrintJob> PrintJobs { get; set; } = [];
}
