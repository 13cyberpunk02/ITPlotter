using ITPlotter.Domain.Interfaces;
using ITPlotter.Domain.PaperOptimization;
using Microsoft.Extensions.Logging;

namespace ITPlotter.Infrastructure.PdfProcessing;

/// <summary>
/// Фасад: скачивает PDF из S3, прогоняет через детекцию формата → растеризацию → оптимизацию → генерацию,
/// загружает результат обратно в S3.
/// </summary>
public class DocumentOptimizationService
{
    private readonly FormatDetector _formatDetector;
    private readonly PdfRasterizer _rasterizer;
    private readonly PrintOptimizer _optimizer;
    private readonly PdfProcessor _pdfProcessor;
    private readonly IStorageService _storage;
    private readonly ILogger<DocumentOptimizationService> _logger;

    public DocumentOptimizationService(
        FormatDetector formatDetector,
        PdfRasterizer rasterizer,
        PrintOptimizer optimizer,
        PdfProcessor pdfProcessor,
        IStorageService storage,
        ILogger<DocumentOptimizationService> logger)
    {
        _formatDetector = formatDetector;
        _rasterizer = rasterizer;
        _optimizer = optimizer;
        _pdfProcessor = pdfProcessor;
        _storage = storage;
        _logger = logger;
    }

    /// <summary>
    /// Оптимизирует один PDF документ из S3.
    /// Возвращает S3-ключ оптимизированного файла и информацию о формате.
    /// </summary>
    public async Task<OptimizationOutput> OptimizeDocumentAsync(string s3Key, string originalFileName, CancellationToken ct = default)
    {
        string workDir = Path.Combine(Path.GetTempPath(), "itplotter", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            // 1. Скачиваем из S3 во временный файл
            string localPdf = Path.Combine(workDir, originalFileName);
            var stream = await _storage.DownloadFileAsync(s3Key, ct);
            await using (var fs = File.Create(localPdf))
            {
                await stream.CopyToAsync(fs, ct);
            }

            _logger.LogInformation("Оптимизация документа: {File}", originalFileName);

            // 2. Детекция формата
            var detected = _formatDetector.DetectFormat(localPdf);

            // 3. Растеризация тяжёлых PDF (AutoCAD и т.д.)
            detected = _rasterizer.ProcessAll(detected, workDir, _formatDetector);

            // 4. Оптимизация (группировка, стратегии склейки)
            var optimizationResult = _optimizer.Optimize(detected);

            // 5. Генерация оптимизированных PDF
            string outputDir = Path.Combine(workDir, "output");
            string tempDir = Path.Combine(workDir, "temp");
            Directory.CreateDirectory(outputDir);
            _pdfProcessor.GenerateOutputFiles(optimizationResult, outputDir, tempDir);

            // 6. Загружаем результаты обратно в S3
            var outputs = new List<OptimizedFileInfo>();
            foreach (var job in optimizationResult.AllJobs)
            {
                if (string.IsNullOrEmpty(job.OutputFilePath) || !File.Exists(job.OutputFilePath))
                    continue;

                await using var fileStream = File.OpenRead(job.OutputFilePath);
                string optimizedKey = await _storage.UploadFileAsync(
                    fileStream,
                    $"optimized_{Path.GetFileName(job.OutputFilePath)}",
                    "application/pdf",
                    ct);

                outputs.Add(new OptimizedFileInfo
                {
                    S3Key = optimizedKey,
                    FileName = Path.GetFileName(job.OutputFilePath),
                    Strategy = job.Strategy.ToString(),
                    ResultWidthMm = job.ResultWidthOnRollMm,
                    ResultLengthMm = job.ResultLengthOnRollMm,
                    PageCount = job.SourceDocuments.Count,
                    WasRasterized = job.SourceDocuments.Any(d => d.WasRasterized)
                });
            }

            // Формируем сводку по обнаруженным форматам
            var formatSummary = detected
                .Where(d => d.Format != null)
                .Select(d => new DetectedPageInfo
                {
                    PageIndex = d.PageIndex,
                    FormatName = d.Format!.Name,
                    WidthMm = d.ActualWidthMm,
                    HeightMm = d.ActualHeightMm,
                    WasRasterized = d.WasRasterized
                })
                .ToList();

            return new OptimizationOutput
            {
                OptimizedFiles = outputs,
                DetectedPages = formatSummary,
                TotalRollLengthMm = optimizationResult.TotalRollLengthMm,
                UnrecognizedPageCount = optimizationResult.UnrecognizedDocuments.Count
            };
        }
        finally
        {
            try { if (Directory.Exists(workDir)) Directory.Delete(workDir, true); }
            catch { }
        }
    }
}

public class OptimizationOutput
{
    public List<OptimizedFileInfo> OptimizedFiles { get; set; } = [];
    public List<DetectedPageInfo> DetectedPages { get; set; } = [];
    public double TotalRollLengthMm { get; set; }
    public int UnrecognizedPageCount { get; set; }
}

public class OptimizedFileInfo
{
    public string S3Key { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Strategy { get; set; } = "";
    public double ResultWidthMm { get; set; }
    public double ResultLengthMm { get; set; }
    public int PageCount { get; set; }
    public bool WasRasterized { get; set; }
}

public class DetectedPageInfo
{
    public int PageIndex { get; set; }
    public string FormatName { get; set; } = "";
    public double WidthMm { get; set; }
    public double HeightMm { get; set; }
    public bool WasRasterized { get; set; }
}
