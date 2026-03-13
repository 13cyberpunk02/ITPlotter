using ITPlotter.Domain.PaperOptimization;

namespace ITPlotter.Infrastructure.PdfProcessing;

public class PrintOptimizer
{
    private int _jobCounter;

    public PlotterOptimizationResult Optimize(List<DetectedDocument> documents)
    {
        _jobCounter = 0;
        var result = new PlotterOptimizationResult();

        var unrecognized = documents.Where(d => d.Format == null).ToList();
        var recognized = documents.Where(d => d.Format != null).ToList();

        result.UnrecognizedDocuments = unrecognized;

        var groups = recognized.GroupBy(d => d.Format!.Group);

        foreach (var group in groups.OrderBy(g => g.Key))
        {
            var groupJobs = OptimizeGroup(group.Key, group.ToList());
            result.JobsByGroup[group.Key] = groupJobs;
        }

        return result;
    }

    private List<PlotterPrintJob> OptimizeGroup(FormatGroup group, List<DetectedDocument> docs)
    {
        var jobs = new List<PlotterPrintJob>();

        var byFormat = docs.GroupBy(d => d.Format!.Name);

        foreach (var formatGroup in byFormat.OrderBy(g => g.Key))
        {
            var format = formatGroup.First().Format!;
            var docsOfFormat = formatGroup.ToList();

            var formatJobs = format.Strategy switch
            {
                PrintStrategy.PrintAsIs => CreatePrintAsIsJobs(format, docsOfFormat),
                PrintStrategy.RotateToFitWidth => CreateRotatedJobs(format, docsOfFormat),
                PrintStrategy.MergeDuplicatesSideBySide => CreateMergeSideBySideJobs(format, docsOfFormat),
                PrintStrategy.RotateAndMergeDuplicates => CreateRotateAndMergeJobs(format, docsOfFormat),
                _ => CreatePrintAsIsJobs(format, docsOfFormat)
            };

            jobs.AddRange(formatJobs);
        }

        return jobs;
    }

    private List<PlotterPrintJob> CreatePrintAsIsJobs(PaperFormat format, List<DetectedDocument> docs)
    {
        return docs.Select(doc => new PlotterPrintJob
        {
            JobNumber = ++_jobCounter,
            Strategy = PrintStrategy.PrintAsIs,
            Description = $"{format.Name} — печать как есть",
            SourceDocuments = [doc],
            ResultWidthOnRollMm = Math.Min(format.WidthMm, format.HeightMm),
            ResultLengthOnRollMm = Math.Max(format.WidthMm, format.HeightMm),
            RequiresRotation = false,
            IsMergedSideBySide = false
        }).ToList();
    }

    private List<PlotterPrintJob> CreateRotatedJobs(PaperFormat format, List<DetectedDocument> docs)
    {
        double shortSide = Math.Min(format.WidthMm, format.HeightMm);
        double longSide = Math.Max(format.WidthMm, format.HeightMm);

        return docs.Select(doc => new PlotterPrintJob
        {
            JobNumber = ++_jobCounter,
            Strategy = PrintStrategy.RotateToFitWidth,
            Description = $"{format.Name} — повёрнут 90°",
            SourceDocuments = [doc],
            ResultWidthOnRollMm = longSide,
            ResultLengthOnRollMm = shortSide,
            RequiresRotation = true,
            IsMergedSideBySide = false
        }).ToList();
    }

    private List<PlotterPrintJob> CreateMergeSideBySideJobs(PaperFormat format, List<DetectedDocument> docs)
    {
        var jobs = new List<PlotterPrintJob>();
        double shortSide = Math.Min(format.WidthMm, format.HeightMm);
        double longSide = Math.Max(format.WidthMm, format.HeightMm);
        double margin = PaperFormat.MergeMarginMm;

        int i = 0;
        while (i + 1 < docs.Count)
        {
            double mergedWidth = shortSide * 2 + margin;
            jobs.Add(new PlotterPrintJob
            {
                JobNumber = ++_jobCounter,
                Strategy = PrintStrategy.MergeDuplicatesSideBySide,
                Description = $"2x {format.Name} — склеены бок-о-бок ({mergedWidth:F0}мм)",
                SourceDocuments = [docs[i], docs[i + 1]],
                ResultWidthOnRollMm = mergedWidth,
                ResultLengthOnRollMm = longSide,
                RequiresRotation = false,
                IsMergedSideBySide = true
            });
            i += 2;
        }

        if (i < docs.Count)
        {
            jobs.Add(new PlotterPrintJob
            {
                JobNumber = ++_jobCounter,
                Strategy = PrintStrategy.MergeDuplicatesSideBySide,
                Description = $"{format.Name} — одиночный",
                SourceDocuments = [docs[i]],
                ResultWidthOnRollMm = shortSide,
                ResultLengthOnRollMm = longSide,
                RequiresRotation = false,
                IsMergedSideBySide = false
            });
        }

        return jobs;
    }

    private List<PlotterPrintJob> CreateRotateAndMergeJobs(PaperFormat format, List<DetectedDocument> docs)
    {
        var jobs = new List<PlotterPrintJob>();
        double shortSide = Math.Min(format.WidthMm, format.HeightMm);
        double longSide = Math.Max(format.WidthMm, format.HeightMm);
        double margin = PaperFormat.MergeMarginMm;

        int i = 0;
        while (i + 1 < docs.Count)
        {
            double mergedLength = shortSide * 2 + margin;
            jobs.Add(new PlotterPrintJob
            {
                JobNumber = ++_jobCounter,
                Strategy = PrintStrategy.RotateAndMergeDuplicates,
                Description = $"2x {format.Name} → {format.MergedFormatName ?? "merged"}",
                SourceDocuments = [docs[i], docs[i + 1]],
                ResultWidthOnRollMm = longSide,
                ResultLengthOnRollMm = mergedLength,
                RequiresRotation = true,
                IsMergedSideBySide = true
            });
            i += 2;
        }

        if (i < docs.Count)
        {
            jobs.Add(new PlotterPrintJob
            {
                JobNumber = ++_jobCounter,
                Strategy = PrintStrategy.RotateAndMergeDuplicates,
                Description = $"{format.Name} — одиночный, повёрнут",
                SourceDocuments = [docs[i]],
                ResultWidthOnRollMm = longSide,
                ResultLengthOnRollMm = shortSide,
                RequiresRotation = true,
                IsMergedSideBySide = false
            });
        }

        return jobs;
    }
}
