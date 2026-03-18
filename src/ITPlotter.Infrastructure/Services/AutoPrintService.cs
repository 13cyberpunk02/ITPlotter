using ITPlotter.Application.Interfaces;
using ITPlotter.Domain.Entities;
using ITPlotter.Domain.Enums;
using ITPlotter.Domain.Interfaces;
using ITPlotter.Infrastructure.PdfProcessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainPaperFormat = ITPlotter.Domain.PaperOptimization.PaperFormat;

namespace ITPlotter.Infrastructure.Services;

public class AutoPrintService
{
    private readonly IApplicationDbContext _db;
    private readonly ICupsService _cups;
    private readonly IStorageService _storage;
    private readonly DocumentOptimizationService _optimization;
    private readonly ILogger<AutoPrintService> _logger;

    public AutoPrintService(
        IApplicationDbContext db,
        ICupsService cups,
        IStorageService storage,
        DocumentOptimizationService optimization,
        ILogger<AutoPrintService> logger)
    {
        _db = db;
        _cups = cups;
        _storage = storage;
        _optimization = optimization;
        _logger = logger;
    }

    /// <summary>
    /// Полный цикл: оптимизация PDF → поиск подходящих принтеров → отправка на печать.
    /// </summary>
    public async Task<AutoPrintResult> ProcessAndPrintAsync(Guid userId, Guid documentId, CancellationToken ct = default)
    {
        var document = await _db.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Документ не найден.");

        if (document.Format != DocumentFormat.Pdf)
            throw new InvalidOperationException("Автоматическая печать доступна только для PDF файлов.");

        // 1. Оптимизация PDF
        _logger.LogInformation("Начало оптимизации документа {FileName}", document.OriginalFileName);
        var optimizationResult = await _optimization.OptimizeDocumentAsync(
            document.S3Key, document.OriginalFileName, ct);

        // 2. Загрузка доступных принтеров
        var printers = await _db.Printers
            .Where(p => p.Status == PrinterStatus.Idle || p.Status == PrinterStatus.Printing)
            .ToListAsync(ct);

        if (printers.Count == 0)
            throw new InvalidOperationException("Нет доступных принтеров.");

        // 3. Для каждого оптимизированного файла определить формат, найти принтер и напечатать
        var jobs = new List<PrintJob>();
        var unmatchedFiles = new List<string>();

        foreach (var file in optimizationResult.OptimizedFiles)
        {
            var requiredFormat = DetectPaperFormat(file);
            var printer = FindBestPrinter(printers, requiredFormat, file);

            if (printer == null)
            {
                _logger.LogWarning("Не найден принтер для формата {Format}, файл {File}",
                    requiredFormat, file.FileName);
                unmatchedFiles.Add(file.FileName);
                continue;
            }

            var fileStream = await _storage.DownloadFileAsync(file.S3Key, ct);

            var cupsJobId = await _cups.PrintFileAsync(
                printer.CupsName, fileStream, file.FileName,
                new PrintJobOptions { Copies = 1, PaperFormat = requiredFormat }, ct);

            var printJob = new PrintJob
            {
                Id = Guid.NewGuid(),
                CupsJobId = cupsJobId,
                Status = PrintJobStatus.Processing,
                Copies = 1,
                PaperFormat = requiredFormat,
                DocumentId = document.Id,
                PrinterId = printer.Id,
                UserId = userId
            };

            _db.PrintJobs.Add(printJob);
            jobs.Add(printJob);

            _logger.LogInformation("Отправлен на печать: {File} -> {Printer} ({Format})",
                file.FileName, printer.Name, requiredFormat);
        }

        await _db.SaveChangesAsync(ct);

        return new AutoPrintResult
        {
            DocumentName = document.OriginalFileName,
            TotalPages = optimizationResult.OptimizedFiles.Count,
            JobsCreated = jobs.Count,
            UnmatchedPages = unmatchedFiles.Count,
            Jobs = jobs.Select(j => new AutoPrintJobInfo
            {
                JobId = j.Id,
                PrinterName = printers.First(p => p.Id == j.PrinterId).Name,
                PaperFormat = j.PaperFormat
            }).ToList()
        };
    }

    private static PaperFormat DetectPaperFormat(OptimizedFileInfo file)
    {
        // Используем класс PaperOptimization.PaperFormat для точного определения формата
        // по размерам оптимизированного файла
        var (domainFormat, _) = DomainPaperFormat.FindClosestFormat(file.ResultWidthMm, file.ResultLengthMm);

        if (domainFormat != null && Enum.TryParse<PaperFormat>(domainFormat.Name, out var enumFormat))
            return enumFormat;

        // Fallback по размерам для нестандартных результатов оптимизации
        double maxDim = Math.Max(file.ResultWidthMm, file.ResultLengthMm);
        double minDim = Math.Min(file.ResultWidthMm, file.ResultLengthMm);

        if (minDim <= 220 && maxDim <= 310) return PaperFormat.A4;
        if (minDim <= 310 && maxDim <= 440) return PaperFormat.A3;
        if (minDim <= 440 && maxDim <= 630) return PaperFormat.A2;
        if (minDim <= 630 && maxDim <= 900) return PaperFormat.A1;
        return PaperFormat.A0;
    }

    /// <summary>
    /// Минимальная ширина рулона (мм), необходимая для данного формата.
    /// </summary>
    private static double GetMinRollWidthMm(PaperFormat format)
    {
        var domainFormat = DomainPaperFormat.KnownFormats.FirstOrDefault(f => f.Name == format.ToString());
        if (domainFormat == null) return 914;
        return Math.Min(domainFormat.WidthMm, domainFormat.HeightMm);
    }

    private static Printer? FindBestPrinter(List<Printer> printers, PaperFormat requiredFormat, OptimizedFileInfo file)
    {
        // Реальная ширина на рулоне определяется из оптимизированного файла
        double neededWidthMm = file.ResultWidthMm;

        // Для стандартных форматов (A4, A3) предпочитаем обычный принтер
        bool isSmallFormat = requiredFormat is PaperFormat.A4 or PaperFormat.A3;

        return printers
            .OrderBy(p => p.Status == PrinterStatus.Idle ? 0 : 1)
            .ThenBy(p => isSmallFormat
                ? (p.Type == PrinterType.Printer ? 0 : 1)
                : (p.Type == PrinterType.Plotter ? 0 : 1))
            .FirstOrDefault();
    }
}

public class AutoPrintResult
{
    public string DocumentName { get; set; } = string.Empty;
    public int TotalPages { get; set; }
    public int JobsCreated { get; set; }
    public int UnmatchedPages { get; set; }
    public List<AutoPrintJobInfo> Jobs { get; set; } = [];
}

public class AutoPrintJobInfo
{
    public Guid JobId { get; set; }
    public string PrinterName { get; set; } = string.Empty;
    public PaperFormat PaperFormat { get; set; }
}
