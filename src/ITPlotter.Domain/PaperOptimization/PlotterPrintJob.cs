namespace ITPlotter.Domain.PaperOptimization;

/// <summary>
/// Задание на печать оптимизатора (не путать с Domain.Entities.PrintJob — сущностью БД).
/// </summary>
public class PlotterPrintJob
{
    public int JobNumber { get; set; }
    public PrintStrategy Strategy { get; set; }
    public string Description { get; set; } = "";
    public List<DetectedDocument> SourceDocuments { get; set; } = [];
    public string OutputFilePath { get; set; } = "";
    public double ResultWidthOnRollMm { get; set; }
    public double ResultLengthOnRollMm { get; set; }
    public bool RequiresRotation { get; set; }
    public bool IsMergedSideBySide { get; set; }
}
