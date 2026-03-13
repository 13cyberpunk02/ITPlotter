using ITPlotter.Domain.Enums;

namespace ITPlotter.Application.DTOs.Documents;

public record DocumentDto(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    DocumentFormat Format,
    DateTime UploadedAt);
