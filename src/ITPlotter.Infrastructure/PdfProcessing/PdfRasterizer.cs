using System.Diagnostics;
using ITPlotter.Domain.PaperOptimization;
using Microsoft.Extensions.Logging;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace ITPlotter.Infrastructure.PdfProcessing;

public class PdfRasterizer
{
    private readonly ILogger<PdfRasterizer> _logger;

    public long FileSizePerPageThreshold { get; set; } = 2 * 1024 * 1024;
    public int ObjectCountThreshold { get; set; } = 3000;
    public long ContentStreamLengthPerPageThreshold { get; set; } = 500_000;
    public int RasterDpi { get; set; } = 200;
    public bool ForceRasterize { get; set; }
    public string? GhostscriptPath { get; set; }

    private string? _resolvedGsPath;

    public PdfRasterizer(ILogger<PdfRasterizer> logger)
    {
        _logger = logger;
    }

    public List<DetectedDocument> ProcessAll(List<DetectedDocument> documents, string workDir, FormatDetector detector)
    {
        _resolvedGsPath = ResolveGhostscriptPath();
        if (_resolvedGsPath == null)
        {
            _logger.LogWarning("Ghostscript не найден, растеризация невозможна");
            return documents;
        }

        _logger.LogInformation("Ghostscript найден: {Path}", _resolvedGsPath);

        var byFile = documents.GroupBy(d => d.FilePath).ToList();
        string rasterDir = Path.Combine(workDir, "_rasterized");
        Directory.CreateDirectory(rasterDir);

        var result = new List<DetectedDocument>();

        foreach (var fileGroup in byFile)
        {
            string filePath = fileGroup.Key;
            var fileDocs = fileGroup.ToList();
            bool isHeavy = ForceRasterize || IsHeavyPdf(filePath);

            if (isHeavy)
            {
                _logger.LogInformation("{File} — тяжёлый PDF, растеризация {Dpi} DPI",
                    Path.GetFileName(filePath), RasterDpi);

                string? rasterizedPath = RasterizePdf(filePath, rasterDir);

                if (rasterizedPath != null)
                {
                    var newDocs = detector.DetectFormat(rasterizedPath);

                    foreach (var origDoc in fileDocs)
                    {
                        var newDoc = newDocs.FirstOrDefault(d => d.PageIndex == origDoc.PageIndex);
                        if (newDoc != null)
                        {
                            result.Add(new DetectedDocument
                            {
                                FilePath = rasterizedPath,
                                Format = origDoc.Format ?? newDoc.Format,
                                MatchScore = origDoc.MatchScore,
                                ActualWidthMm = newDoc.ActualWidthMm,
                                ActualHeightMm = newDoc.ActualHeightMm,
                                ActualWidthPt = newDoc.ActualWidthPt,
                                ActualHeightPt = newDoc.ActualHeightPt,
                                OriginalRotation = newDoc.OriginalRotation,
                                PageIndex = origDoc.PageIndex,
                                WasRasterized = true,
                                OriginalFileName = Path.GetFileName(filePath)
                            });
                        }
                        else
                        {
                            result.Add(origDoc);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Ошибка растеризации {File}, используем оригинал", Path.GetFileName(filePath));
                    result.AddRange(fileDocs);
                }
            }
            else
            {
                result.AddRange(fileDocs);
            }
        }

        return result;
    }

    public bool IsHeavyPdf(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            using var document = PdfReader.Open(filePath, PdfDocumentOpenMode.ReadOnly);
            int pageCount = Math.Max(document.PageCount, 1);

            long sizePerPage = fileInfo.Length / pageCount;
            if (sizePerPage > FileSizePerPageThreshold)
                return true;

            int objectCount = CountPdfObjects(filePath);
            if (objectCount > ObjectCountThreshold)
                return true;

            long totalStreamLength = EstimateContentStreamLength(document);
            long streamPerPage = totalStreamLength / pageCount;
            if (streamPerPage > ContentStreamLengthPerPageThreshold)
                return true;

            return false;
        }
        catch
        {
            return true;
        }
    }

    private int CountPdfObjects(string filePath)
    {
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            int count = 0;

            for (int i = 0; i < bytes.Length - 6; i++)
            {
                if (bytes[i] >= (byte)'0' && bytes[i] <= (byte)'9' &&
                    bytes[i + 1] == (byte)' ' &&
                    bytes[i + 2] >= (byte)'0' && bytes[i + 2] <= (byte)'9' &&
                    bytes[i + 3] == (byte)' ' &&
                    bytes[i + 4] == (byte)'o' &&
                    bytes[i + 5] == (byte)'b' &&
                    bytes[i + 6] == (byte)'j')
                {
                    count++;
                    i += 6;
                }
            }

            return count;
        }
        catch { return 0; }
    }

    private long EstimateContentStreamLength(PdfDocument document)
    {
        long total = 0;
        try
        {
            for (int i = 0; i < document.PageCount; i++)
            {
                var page = document.Pages[i];
                var contents = page.Elements.GetObject("/Contents");

                if (contents is PdfSharpCore.Pdf.PdfArray array)
                    total += array.Elements.Count * 50_000;
                else if (contents is PdfSharpCore.Pdf.Advanced.PdfDictionaryWithContentStream dict)
                {
                    var lengthObj = dict.Elements.GetInteger("/Length");
                    total += lengthObj > 0 ? lengthObj : 100_000;
                }
                else
                    total += 100_000;
            }
        }
        catch
        {
            total = 100_000 * document.PageCount;
        }
        return total;
    }

    private string? RasterizePdf(string inputPath, string outputDir)
    {
        try
        {
            string outputPath = Path.Combine(outputDir, $"raster_{Path.GetFileNameWithoutExtension(inputPath)}.pdf");

            string args = $"-dNOPAUSE -dBATCH -dSAFER -dQUIET " +
                          $"-sDEVICE=pdfimage24 " +
                          $"-r{RasterDpi} " +
                          $"-dCompatibilityLevel=1.4 " +
                          $"-dDownScaleFactor=1 " +
                          $"-o \"{outputPath}\" " +
                          $"\"{inputPath}\"";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _resolvedGsPath!,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.StandardError.ReadToEnd();
            process.WaitForExit(timeout: TimeSpan.FromMinutes(2));

            if (process.ExitCode != 0 || !File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
                return RasterizePdfFallback(inputPath, outputDir);

            return outputPath;
        }
        catch
        {
            return RasterizePdfFallback(inputPath, outputDir);
        }
    }

    private string? RasterizePdfFallback(string inputPath, string outputDir)
    {
        try
        {
            string tempDir = Path.Combine(outputDir, $"_temp_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            string pngPattern = Path.Combine(tempDir, "page_%04d.png");

            string argsToImage = $"-dNOPAUSE -dBATCH -dSAFER -dQUIET " +
                                 $"-sDEVICE=png16m " +
                                 $"-r{RasterDpi} " +
                                 $"-o \"{pngPattern}\" " +
                                 $"\"{inputPath}\"";

            var process = RunGhostscript(argsToImage);
            if (process == null || process.ExitCode != 0)
            {
                CleanupDir(tempDir);
                return null;
            }

            var pngFiles = Directory.GetFiles(tempDir, "page_*.png").OrderBy(f => f).ToArray();
            if (pngFiles.Length == 0)
            {
                CleanupDir(tempDir);
                return null;
            }

            string outputPath = Path.Combine(outputDir, $"raster_{Path.GetFileNameWithoutExtension(inputPath)}.pdf");

            using var outputPdf = new PdfDocument();
            using var origDoc = PdfReader.Open(inputPath, PdfDocumentOpenMode.ReadOnly);

            for (int i = 0; i < pngFiles.Length; i++)
            {
                double pageWidthPt, pageHeightPt;
                if (i < origDoc.PageCount)
                {
                    var origPage = origDoc.Pages[i];
                    pageWidthPt = origPage.MediaBox.Width;
                    pageHeightPt = origPage.MediaBox.Height;
                    if (origPage.Rotate == 90 || origPage.Rotate == 270)
                        (pageWidthPt, pageHeightPt) = (pageHeightPt, pageWidthPt);
                }
                else
                {
                    pageWidthPt = 595;
                    pageHeightPt = 842;
                }

                var newPage = outputPdf.AddPage();
                newPage.Width = new XUnit(pageWidthPt, XGraphicsUnit.Point);
                newPage.Height = new XUnit(pageHeightPt, XGraphicsUnit.Point);

                using var gfx = XGraphics.FromPdfPage(newPage);
                using var img = XImage.FromFile(pngFiles[i]);
                gfx.DrawImage(img, 0, 0, pageWidthPt, pageHeightPt);
            }

            outputPdf.Save(outputPath);
            CleanupDir(tempDir);

            return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0 ? outputPath : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallback растеризация провалилась");
            return null;
        }
    }

    private Process? RunGhostscript(string args)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _resolvedGsPath!,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.StandardError.ReadToEnd();
            process.WaitForExit(timeout: TimeSpan.FromMinutes(3));
            return process;
        }
        catch { return null; }
    }

    private string? ResolveGhostscriptPath()
    {
        if (!string.IsNullOrEmpty(GhostscriptPath) && File.Exists(GhostscriptPath))
            return GhostscriptPath;

        string[] candidates = OperatingSystem.IsWindows()
            ? ["gswin64c.exe", "gswin32c.exe", "gs.exe"]
            : ["gs", "/usr/bin/gs", "/usr/local/bin/gs", "/opt/homebrew/bin/gs"];

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;

            if (!Path.IsPathRooted(candidate))
            {
                try
                {
                    string whichCmd = OperatingSystem.IsWindows() ? "where" : "which";
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = whichCmd,
                            Arguments = candidate,
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(5000);
                    if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                    {
                        string firstLine = output.Split('\n').First().Trim();
                        if (File.Exists(firstLine))
                            return firstLine;
                    }
                }
                catch { }
            }
        }

        return null;
    }

    private static void CleanupDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        catch { }
    }
}
