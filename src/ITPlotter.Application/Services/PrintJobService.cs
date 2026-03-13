using ITPlotter.Application.DTOs.PrintJobs;
using ITPlotter.Application.Interfaces;
using ITPlotter.Domain.Entities;
using ITPlotter.Domain.Enums;
using ITPlotter.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITPlotter.Application.Services;

public class PrintJobService
{
    private readonly IApplicationDbContext _db;
    private readonly ICupsService _cups;
    private readonly IStorageService _storage;

    public PrintJobService(IApplicationDbContext db, ICupsService cups, IStorageService storage)
    {
        _db = db;
        _cups = cups;
        _storage = storage;
    }

    public async Task<PrintJobDto> CreateAsync(Guid userId, CreatePrintJobRequest request, CancellationToken ct = default)
    {
        var document = await _db.Documents.FirstOrDefaultAsync(d => d.Id == request.DocumentId && d.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Документ не найден.");

        var printer = await _db.Printers.FindAsync([request.PrinterId], ct)
            ?? throw new KeyNotFoundException("Принтер не найден.");

        if (printer.Status is PrinterStatus.PaperJam or PrinterStatus.OutOfPaper
            or PrinterStatus.OutOfToner or PrinterStatus.OutOfInk
            or PrinterStatus.Error or PrinterStatus.Offline)
        {
            throw new InvalidOperationException($"Принтер недоступен. Статус: {printer.Status}");
        }

        var fileStream = await _storage.DownloadFileAsync(document.S3Key, ct);

        var cupsJobId = await _cups.PrintFileAsync(
            printer.CupsName, fileStream, document.OriginalFileName,
            new PrintJobOptions { Copies = request.Copies, PaperFormat = request.PaperFormat }, ct);

        var printJob = new PrintJob
        {
            Id = Guid.NewGuid(),
            CupsJobId = cupsJobId,
            Status = PrintJobStatus.Processing,
            Copies = request.Copies,
            PaperFormat = request.PaperFormat,
            DocumentId = document.Id,
            PrinterId = printer.Id,
            UserId = userId
        };

        _db.PrintJobs.Add(printJob);
        await _db.SaveChangesAsync(ct);

        return ToDto(printJob, document.OriginalFileName, printer.Name);
    }

    public async Task<List<PrintJobDto>> GetUserJobsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.PrintJobs
            .Where(j => j.UserId == userId)
            .Include(j => j.Document)
            .Include(j => j.Printer)
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new PrintJobDto(
                j.Id, j.CupsJobId, j.Status, j.Copies, j.PaperFormat,
                j.CreatedAt, j.CompletedAt, j.ErrorMessage,
                j.Document.OriginalFileName, j.Printer.Name))
            .ToListAsync(ct);
    }

    public async Task<PrintJobDto> GetJobStatusAsync(Guid userId, Guid jobId, CancellationToken ct = default)
    {
        var job = await _db.PrintJobs
            .Include(j => j.Document)
            .Include(j => j.Printer)
            .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Задание печати не найдено.");

        if (job.Status is PrintJobStatus.Processing or PrintJobStatus.Printing)
        {
            var cupsJob = await _cups.GetJobStatusAsync(job.CupsJobId, ct);
            if (cupsJob is not null)
            {
                job.Status = cupsJob.State switch
                {
                    "completed" => PrintJobStatus.Completed,
                    "cancelled" or "aborted" => PrintJobStatus.Failed,
                    "processing" => PrintJobStatus.Printing,
                    _ => job.Status
                };

                if (job.Status == PrintJobStatus.Completed)
                    job.CompletedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync(ct);
            }
        }

        return ToDto(job, job.Document.OriginalFileName, job.Printer.Name);
    }

    public async Task CancelAsync(Guid userId, Guid jobId, CancellationToken ct = default)
    {
        var job = await _db.PrintJobs.FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Задание печати не найдено.");

        if (job.Status is PrintJobStatus.Completed or PrintJobStatus.Failed or PrintJobStatus.Cancelled)
            throw new InvalidOperationException("Невозможно отменить задание в текущем статусе.");

        await _cups.CancelJobAsync(job.CupsJobId, ct);
        job.Status = PrintJobStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
    }

    private static PrintJobDto ToDto(PrintJob j, string documentName, string printerName) =>
        new(j.Id, j.CupsJobId, j.Status, j.Copies, j.PaperFormat,
            j.CreatedAt, j.CompletedAt, j.ErrorMessage, documentName, printerName);
}
