namespace ITPlotter.Domain.PaperOptimization;

public class DetectedDocument
{
    public string FilePath { get; set; } = "";
    public string FileName => Path.GetFileName(FilePath);
    public PaperFormat? Format { get; set; }
    public double MatchScore { get; set; }
    public double ActualWidthMm { get; set; }
    public double ActualHeightMm { get; set; }
    public double ActualWidthPt { get; set; }
    public double ActualHeightPt { get; set; }
    public int PageIndex { get; set; }
    public bool IsLandscape => ActualWidthMm > ActualHeightMm;
    public int OriginalRotation { get; set; }
    public bool WasRasterized { get; set; }
    public string? OriginalFileName { get; set; }
    public string DisplayName => OriginalFileName ?? FileName;
}
