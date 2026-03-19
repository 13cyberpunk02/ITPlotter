using ITPlotter.Domain.PaperOptimization;
using Microsoft.Extensions.Logging;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace ITPlotter.Infrastructure.PdfProcessing;

public class PdfProcessor
{
    private static readonly double MarginPt = PaperFormat.MergeMarginMm * PaperFormat.MmToPoint;
    private readonly ILogger<PdfProcessor> _logger;

    public PdfProcessor(ILogger<PdfProcessor> logger)
    {
        _logger = logger;
    }

    public void GenerateOutputFiles(PlotterOptimizationResult result, string outputDirectory, string tempDir)
    {
        Directory.CreateDirectory(tempDir);

        try
        {
            foreach (var (group, jobs) in result.JobsByGroup)
            {
                foreach (var job in jobs)
                {
                    try
                    {
                        string outputPath = GenerateJobPdf(job, outputDirectory, tempDir);
                        job.OutputFilePath = outputPath;
                        _logger.LogInformation("Job #{JobNumber}: {File}", job.JobNumber, Path.GetFileName(outputPath));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Job #{JobNumber}: ошибка генерации PDF", job.JobNumber);
                    }
                }
            }
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            catch { }
        }
    }

    private string GenerateJobPdf(PlotterPrintJob job, string outputDir, string tempDir)
    {
        string fileName = BuildOutputFileName(job);
        string outputPath = Path.Combine(outputDir, fileName);

        if (job.IsMergedSideBySide)
        {
            if (job.RequiresRotation)
                CreateRotatedMergedPdf(job, outputPath, tempDir);
            else
                CreateSideBySidePdf(job, outputPath, tempDir);
        }
        else if (job.RequiresRotation)
        {
            CreateRotatedPdf(job, outputPath);
        }
        else
        {
            CreateAsIsPdf(job, outputPath);
        }

        return outputPath;
    }

    private string CreateStrippedPage(DetectedDocument doc, string tempDir)
    {
        string tempPath = Path.Combine(tempDir,
            $"strip_{Path.GetFileNameWithoutExtension(doc.FileName)}_p{doc.PageIndex}_{Guid.NewGuid().ToString("N")[..8]}.pdf");

        using var source = PdfReader.Open(doc.FilePath, PdfDocumentOpenMode.Import);
        using var output = new PdfDocument();
        var page = output.AddPage(source.Pages[doc.PageIndex]);
        page.Rotate = 0;
        output.Save(tempPath);
        return tempPath;
    }

    private void DrawFormWithRotation(XGraphics gfx, XPdfForm form,
                                       int rotation, double x, double y,
                                       double visW, double visH)
    {
        double formW = form.PointWidth;
        double formH = form.PointHeight;
        var state = gfx.Save();

        switch (rotation % 360)
        {
            case 0:
                gfx.DrawImage(form, x, y, formW, formH);
                break;
            case 90:
                gfx.TranslateTransform(x + visW, y);
                gfx.RotateTransform(90);
                gfx.DrawImage(form, 0, 0, formW, formH);
                break;
            case 180:
                gfx.TranslateTransform(x + visW, y + visH);
                gfx.RotateTransform(180);
                gfx.DrawImage(form, 0, 0, formW, formH);
                break;
            case 270:
                gfx.TranslateTransform(x, y + visH);
                gfx.RotateTransform(270);
                gfx.DrawImage(form, 0, 0, formW, formH);
                break;
        }

        gfx.Restore(state);
    }

    private (double visW, double visH) GetVisualSize(double formW, double formH, int rotation)
    {
        if (rotation == 90 || rotation == 270)
            return (formH, formW);
        return (formW, formH);
    }

    private void CreateAsIsPdf(PlotterPrintJob job, string outputPath)
    {
        var doc = job.SourceDocuments.First();
        using var sourceDoc = PdfReader.Open(doc.FilePath, PdfDocumentOpenMode.Import);
        using var output = new PdfDocument();
        output.AddPage(sourceDoc.Pages[doc.PageIndex]);
        output.Save(outputPath);
    }

    private void CreateRotatedPdf(PlotterPrintJob job, string outputPath)
    {
        var doc = job.SourceDocuments.First();
        using var sourceDoc = PdfReader.Open(doc.FilePath, PdfDocumentOpenMode.Import);
        var sourcePage = sourceDoc.Pages[doc.PageIndex];

        double wPt = sourcePage.Width.Point;
        double hPt = sourcePage.Height.Point;
        int rot = sourcePage.Rotate;

        // Визуальные размеры с учётом Rotate
        var (visW, visH) = GetVisualSize(wPt, hPt, rot);

        // Определяем: визуально ландшафт или портрет
        bool isVisualLandscape = visW > visH;

        using var output = new PdfDocument();

        if (isVisualLandscape)
        {
            // Уже визуально ландшафт — копируем страницу как есть.
            // CUPS получит explicit media size, поэтому Rotate флаг не мешает.
            output.AddPage(sourcePage);
        }
        else
        {
            // Визуально портрет — добавляем поворот через Rotate флаг.
            // Это поворачивает всю страницу целиком (и фон, и чертёж).
            var page = output.AddPage(sourcePage);
            page.Rotate = (rot + 90) % 360;
        }

        output.Save(outputPath);
    }

    private void CreateSideBySidePdf(PlotterPrintJob job, string outputPath, string tempDir)
    {
        var docs = job.SourceDocuments;
        if (docs.Count == 1)
        {
            CreateSingleNormalizedPdf(docs[0], outputPath, portrait: true);
            return;
        }

        using var output = new PdfDocument();
        var pageInfos = new List<PageDrawInfo>();

        foreach (var doc in docs)
        {
            string stripped = CreateStrippedPage(doc, tempDir);
            using var form = XPdfForm.FromFile(stripped);
            double formW = form.PointWidth;
            double formH = form.PointHeight;
            int rot = doc.OriginalRotation;
            var (visW, visH) = GetVisualSize(formW, formH, rot);

            bool needsExtraRotation = visW > visH;
            int effectiveRot;
            double slotW, slotH;

            if (needsExtraRotation)
            {
                effectiveRot = (rot + 90) % 360;
                slotW = visH;
                slotH = visW;
            }
            else
            {
                effectiveRot = rot;
                slotW = visW;
                slotH = visH;
            }

            pageInfos.Add(new PageDrawInfo(stripped, formW, formH, effectiveRot, slotW, slotH));
        }

        double totalW = pageInfos.Sum(p => p.SlotW) + MarginPt * (docs.Count - 1);
        double totalH = pageInfos.Max(p => p.SlotH);

        var newPage = output.AddPage();
        newPage.Width = new XUnit(totalW, XGraphicsUnit.Point);
        newPage.Height = new XUnit(totalH, XGraphicsUnit.Point);

        using var gfx = XGraphics.FromPdfPage(newPage);
        double offsetX = 0;

        foreach (var info in pageInfos)
        {
            using var form = XPdfForm.FromFile(info.StrippedPath);
            DrawFormWithRotation(gfx, form, info.EffectiveRotation, offsetX, 0, info.SlotW, info.SlotH);
            offsetX += info.SlotW + MarginPt;
        }

        output.Save(outputPath);

        foreach (var info in pageInfos)
            TryDelete(info.StrippedPath);
    }

    private void CreateRotatedMergedPdf(PlotterPrintJob job, string outputPath, string tempDir)
    {
        var docs = job.SourceDocuments;
        if (docs.Count == 1)
        {
            CreateRotatedPdf(job, outputPath);
            return;
        }

        using var output = new PdfDocument();
        var pageInfos = new List<PageDrawInfo>();

        foreach (var doc in docs)
        {
            string stripped = CreateStrippedPage(doc, tempDir);
            using var form = XPdfForm.FromFile(stripped);
            double formW = form.PointWidth;
            double formH = form.PointHeight;
            int rot = doc.OriginalRotation;
            var (visW, visH) = GetVisualSize(formW, formH, rot);

            bool needsExtraRotation = visH > visW;
            int effectiveRot;
            double slotW, slotH;

            if (needsExtraRotation)
            {
                effectiveRot = (rot + 90) % 360;
                slotW = visH;
                slotH = visW;
            }
            else
            {
                effectiveRot = rot;
                slotW = visW;
                slotH = visH;
            }

            pageInfos.Add(new PageDrawInfo(stripped, formW, formH, effectiveRot, slotW, slotH));
        }

        double resultW = pageInfos.Max(p => p.SlotW);
        double resultH = pageInfos.Sum(p => p.SlotH) + MarginPt * (docs.Count - 1);

        var newPage = output.AddPage();
        newPage.Width = new XUnit(resultW, XGraphicsUnit.Point);
        newPage.Height = new XUnit(resultH, XGraphicsUnit.Point);

        using var gfx = XGraphics.FromPdfPage(newPage);
        double offsetY = 0;

        foreach (var info in pageInfos)
        {
            using var form = XPdfForm.FromFile(info.StrippedPath);
            DrawFormWithRotation(gfx, form, info.EffectiveRotation, 0, offsetY, info.SlotW, info.SlotH);
            offsetY += info.SlotH + MarginPt;
        }

        output.Save(outputPath);

        foreach (var info in pageInfos)
            TryDelete(info.StrippedPath);
    }

    private void CreateSingleNormalizedPdf(DetectedDocument doc, string outputPath, bool portrait)
    {
        using var sourceDoc = PdfReader.Open(doc.FilePath, PdfDocumentOpenMode.Import);
        using var output = new PdfDocument();
        var page = output.AddPage(sourceDoc.Pages[doc.PageIndex]);

        double wPt = page.Width.Point;
        double hPt = page.Height.Point;
        int rot = page.Rotate;

        bool isLandscape = (rot == 90 || rot == 270) ? hPt > wPt : wPt > hPt;

        if (portrait && isLandscape)
            page.Rotate = (page.Rotate + 90) % 360;
        else if (!portrait && !isLandscape)
            page.Rotate = (page.Rotate + 90) % 360;

        output.Save(outputPath);
    }

    private record PageDrawInfo(
        string StrippedPath, double FormW, double FormH,
        int EffectiveRotation, double SlotW, double SlotH);

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }

    private static string BuildOutputFileName(PlotterPrintJob job)
    {
        string prefix = $"{job.JobNumber:D3}";
        var docs = job.SourceDocuments;

        if (docs.Count == 1)
        {
            string baseName = Path.GetFileNameWithoutExtension(docs[0].FileName);
            string suffix = job.RequiresRotation ? "_rotated" : "";
            return $"{prefix}_{baseName}{suffix}.pdf";
        }

        string name1 = Path.GetFileNameWithoutExtension(docs[0].FileName);
        string name2 = Path.GetFileNameWithoutExtension(docs[1].FileName);
        string formatName = docs[0].Format?.Name ?? "unknown";
        return $"{prefix}_{formatName}_merged_{name1}+{name2}.pdf";
    }
}
