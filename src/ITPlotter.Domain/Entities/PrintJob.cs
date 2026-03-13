using ITPlotter.Domain.Enums;

namespace ITPlotter.Domain.Entities;

public class PrintJob
{
    public Guid Id { get; set; }
    public int CupsJobId { get; set; }
    public PrintJobStatus Status { get; set; } = PrintJobStatus.Pending;
    public int Copies { get; set; } = 1;
    public PaperFormat PaperFormat { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;

    public Guid PrinterId { get; set; }
    public Printer Printer { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
