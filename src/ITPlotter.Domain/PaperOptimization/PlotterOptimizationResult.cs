namespace ITPlotter.Domain.PaperOptimization;

public class PlotterOptimizationResult
{
    public Dictionary<FormatGroup, List<PlotterPrintJob>> JobsByGroup { get; set; } = new();
    public List<DetectedDocument> UnrecognizedDocuments { get; set; } = [];
    public IEnumerable<PlotterPrintJob> AllJobs => JobsByGroup.Values.SelectMany(j => j);
    public double TotalRollLengthMm => AllJobs.Sum(j => j.ResultLengthOnRollMm);
}
