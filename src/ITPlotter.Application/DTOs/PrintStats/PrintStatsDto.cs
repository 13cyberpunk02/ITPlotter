using ITPlotter.Domain.Enums;

namespace ITPlotter.Application.DTOs.PrintStats;

public record PrintStatsDto(
    int TotalJobs,
    int TotalPages,
    List<FormatStatsDto> ByFormat,
    List<DailyStatsDto> Recent);

public record FormatStatsDto(PaperFormat Format, int Pages);

public record DailyStatsDto(DateOnly Date, int Pages);
