using ITPlotter.Domain.Enums;

namespace ITPlotter.Domain.Entities;

public class Document
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string S3Key { get; set; } = string.Empty;
    public DocumentFormat Format { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public ICollection<PrintJob> PrintJobs { get; set; } = [];
}
