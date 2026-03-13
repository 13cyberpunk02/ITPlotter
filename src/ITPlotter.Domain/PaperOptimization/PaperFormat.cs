namespace ITPlotter.Domain.PaperOptimization;

public class PaperFormat
{
    public string Name { get; init; } = "";
    public double WidthMm { get; init; }
    public double HeightMm { get; init; }
    public FormatGroup Group { get; init; }
    public PrintStrategy Strategy { get; init; }
    public string? MergedFormatName { get; init; }

    public const double MaxTolerancePercent = 0.10;
    public const double MergeMarginMm = 5.0;
    public const double RollWidthMm = 914.0;
    public const double RollLengthMm = 175_000.0;
    public const double MmToPoint = 72.0 / 25.4;
    public const double PointToMm = 25.4 / 72.0;

    public static readonly PaperFormat[] KnownFormats =
    {
        new() { Name = "A4",   WidthMm = 210, HeightMm = 297,  Group = FormatGroup.A4, Strategy = PrintStrategy.PrintAsIs },
        new() { Name = "A3",   WidthMm = 297, HeightMm = 420,  Group = FormatGroup.A3, Strategy = PrintStrategy.PrintAsIs },

        new() { Name = "A4x3", WidthMm = 297, HeightMm = 630,  Group = FormatGroup.Large, Strategy = PrintStrategy.RotateToFitWidth },
        new() { Name = "A4x4", WidthMm = 297, HeightMm = 841,  Group = FormatGroup.Large, Strategy = PrintStrategy.RotateToFitWidth },
        new() { Name = "A4x5", WidthMm = 297, HeightMm = 1051, Group = FormatGroup.Large, Strategy = PrintStrategy.MergeDuplicatesSideBySide },
        new() { Name = "A4x6", WidthMm = 297, HeightMm = 1261, Group = FormatGroup.Large, Strategy = PrintStrategy.MergeDuplicatesSideBySide },
        new() { Name = "A4x7", WidthMm = 297, HeightMm = 1471, Group = FormatGroup.Large, Strategy = PrintStrategy.MergeDuplicatesSideBySide },
        new() { Name = "A4x8", WidthMm = 297, HeightMm = 1682, Group = FormatGroup.Large, Strategy = PrintStrategy.MergeDuplicatesSideBySide },
        new() { Name = "A4x9", WidthMm = 297, HeightMm = 1892, Group = FormatGroup.Large, Strategy = PrintStrategy.MergeDuplicatesSideBySide },

        new() { Name = "A3x3", WidthMm = 420, HeightMm = 891,  Group = FormatGroup.Large, Strategy = PrintStrategy.MergeDuplicatesSideBySide },
        new() { Name = "A3x4", WidthMm = 420, HeightMm = 1189, Group = FormatGroup.Large, Strategy = PrintStrategy.MergeDuplicatesSideBySide },
        new() { Name = "A3x5", WidthMm = 420, HeightMm = 1486, Group = FormatGroup.Large, Strategy = PrintStrategy.MergeDuplicatesSideBySide },
        new() { Name = "A3x6", WidthMm = 420, HeightMm = 1783, Group = FormatGroup.Large, Strategy = PrintStrategy.MergeDuplicatesSideBySide },
        new() { Name = "A3x7", WidthMm = 420, HeightMm = 2080, Group = FormatGroup.Large, Strategy = PrintStrategy.MergeDuplicatesSideBySide },

        new() { Name = "A2",   WidthMm = 420, HeightMm = 594,  Group = FormatGroup.Large, Strategy = PrintStrategy.RotateAndMergeDuplicates, MergedFormatName = "A1" },
        new() { Name = "A2x3", WidthMm = 594, HeightMm = 1261, Group = FormatGroup.Large, Strategy = PrintStrategy.PrintAsIs },
        new() { Name = "A2x4", WidthMm = 594, HeightMm = 1682, Group = FormatGroup.Large, Strategy = PrintStrategy.PrintAsIs },
        new() { Name = "A2x5", WidthMm = 594, HeightMm = 2102, Group = FormatGroup.Large, Strategy = PrintStrategy.PrintAsIs },

        new() { Name = "A1",   WidthMm = 594, HeightMm = 841,  Group = FormatGroup.Large, Strategy = PrintStrategy.RotateToFitWidth },
        new() { Name = "A1x3", WidthMm = 841, HeightMm = 1783, Group = FormatGroup.Large, Strategy = PrintStrategy.PrintAsIs },
        new() { Name = "A1x4", WidthMm = 841, HeightMm = 2378, Group = FormatGroup.Large, Strategy = PrintStrategy.PrintAsIs },

        new() { Name = "A0",   WidthMm = 841, HeightMm = 1189, Group = FormatGroup.Large, Strategy = PrintStrategy.PrintAsIs },
        new() { Name = "A0x2", WidthMm = 1189, HeightMm = 1682, Group = FormatGroup.Large, Strategy = PrintStrategy.PrintAsIs },
        new() { Name = "A0x3", WidthMm = 1189, HeightMm = 2523, Group = FormatGroup.Large, Strategy = PrintStrategy.PrintAsIs },
    };

    public double? GetMatchScore(double widthMm, double heightMm)
    {
        double w = Math.Min(widthMm, heightMm);
        double h = Math.Max(widthMm, heightMm);

        double expectedW = Math.Min(WidthMm, HeightMm);
        double expectedH = Math.Max(WidthMm, HeightMm);

        double deviationW = expectedW > 0 ? Math.Abs(w - expectedW) / expectedW : double.MaxValue;
        double deviationH = expectedH > 0 ? Math.Abs(h - expectedH) / expectedH : double.MaxValue;

        if (deviationW > MaxTolerancePercent || deviationH > MaxTolerancePercent)
            return null;

        return Math.Sqrt(deviationW * deviationW + deviationH * deviationH);
    }

    public static (PaperFormat? format, double score) FindClosestFormat(double widthMm, double heightMm)
    {
        PaperFormat? best = null;
        double bestScore = double.MaxValue;

        foreach (var format in KnownFormats)
        {
            var score = format.GetMatchScore(widthMm, heightMm);
            if (score.HasValue && score.Value < bestScore)
            {
                bestScore = score.Value;
                best = format;
            }
        }

        return (best, best != null ? bestScore : 0);
    }

    public override string ToString() => $"{Name} ({WidthMm}x{HeightMm}мм)";
}
