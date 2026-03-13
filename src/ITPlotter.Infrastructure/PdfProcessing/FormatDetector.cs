using ITPlotter.Domain.PaperOptimization;
using Microsoft.Extensions.Logging;
using PdfSharpCore.Pdf.IO;

namespace ITPlotter.Infrastructure.PdfProcessing;

public class FormatDetector
{
    private readonly ILogger<FormatDetector> _logger;

    public FormatDetector(ILogger<FormatDetector> logger)
    {
        _logger = logger;
    }

    public List<DetectedDocument> DetectFormat(string filePath)
    {
        var results = new List<DetectedDocument>();

        using var document = PdfReader.Open(filePath, PdfDocumentOpenMode.Import);

        for (int i = 0; i < document.PageCount; i++)
        {
            var page = document.Pages[i];
            int rotation = page.Rotate;

            var candidates = new List<(double wPt, double hPt, string source)>();

            if (page.MediaBox.Width > 0 && page.MediaBox.Height > 0)
                candidates.Add((page.MediaBox.Width, page.MediaBox.Height, "MediaBox"));

            try
            {
                var cropBox = page.CropBox;
                if (cropBox.Width > 0 && cropBox.Height > 0 &&
                    (Math.Abs(cropBox.Width - page.MediaBox.Width) > 1 ||
                     Math.Abs(cropBox.Height - page.MediaBox.Height) > 1))
                {
                    candidates.Add((cropBox.Width, cropBox.Height, "CropBox"));
                }
            }
            catch { }

            try
            {
                var trimBox = page.TrimBox;
                if (trimBox.Width > 0 && trimBox.Height > 0 &&
                    (Math.Abs(trimBox.Width - page.MediaBox.Width) > 1 ||
                     Math.Abs(trimBox.Height - page.MediaBox.Height) > 1))
                {
                    candidates.Add((trimBox.Width, trimBox.Height, "TrimBox"));
                }
            }
            catch { }

            PaperFormat? bestFormat = null;
            double bestScore = double.MaxValue;
            double bestWidthPt = 0, bestHeightPt = 0;
            double bestWidthMm = 0, bestHeightMm = 0;

            foreach (var (wPt, hPt, source) in candidates)
            {
                double wAdjusted = wPt;
                double hAdjusted = hPt;

                if (rotation == 90 || rotation == 270)
                    (wAdjusted, hAdjusted) = (hAdjusted, wAdjusted);

                double wMm = wAdjusted * PaperFormat.PointToMm;
                double hMm = hAdjusted * PaperFormat.PointToMm;

                var (format, score) = PaperFormat.FindClosestFormat(wMm, hMm);

                if (format != null && score < bestScore)
                {
                    bestFormat = format;
                    bestScore = score;
                    bestWidthPt = wAdjusted;
                    bestHeightPt = hAdjusted;
                    bestWidthMm = wMm;
                    bestHeightMm = hMm;
                }
            }

            if (bestFormat == null && candidates.Count > 0)
            {
                var (wPt, hPt, _) = candidates[0];
                if (rotation == 90 || rotation == 270)
                    (wPt, hPt) = (hPt, wPt);

                bestWidthPt = wPt;
                bestHeightPt = hPt;
                bestWidthMm = wPt * PaperFormat.PointToMm;
                bestHeightMm = hPt * PaperFormat.PointToMm;
            }

            var detected = new DetectedDocument
            {
                FilePath = filePath,
                Format = bestFormat,
                MatchScore = bestFormat != null ? bestScore : 0,
                ActualWidthMm = bestWidthMm,
                ActualHeightMm = bestHeightMm,
                ActualWidthPt = bestWidthPt,
                ActualHeightPt = bestHeightPt,
                OriginalRotation = rotation,
                PageIndex = i
            };

            if (bestFormat != null)
                _logger.LogDebug("{File} p.{Page}: {W:F1}x{H:F1}мм → {Format}",
                    detected.FileName, i + 1, bestWidthMm, bestHeightMm, bestFormat.Name);
            else
                _logger.LogWarning("{File} p.{Page}: {W:F1}x{H:F1}мм — формат не распознан",
                    detected.FileName, i + 1, bestWidthMm, bestHeightMm);

            results.Add(detected);
        }

        return results;
    }
}
