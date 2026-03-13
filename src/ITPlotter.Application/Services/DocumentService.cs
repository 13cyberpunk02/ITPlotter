using ITPlotter.Application.DTOs.Documents;
using ITPlotter.Application.Interfaces;
using ITPlotter.Domain.Entities;
using ITPlotter.Domain.Enums;
using ITPlotter.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ITPlotter.Application.Services;

public class DocumentService
{
    private static readonly HashSet<string> AllowedExtensions = [".doc", ".docx", ".pdf"];
    private static readonly Dictionary<string, DocumentFormat> ExtensionToFormat = new()
    {
        [".pdf"] = DocumentFormat.Pdf,
        [".doc"] = DocumentFormat.Doc,
        [".docx"] = DocumentFormat.Docx
    };

    private readonly IApplicationDbContext _db;
    private readonly IStorageService _storage;

    public DocumentService(IApplicationDbContext db, IStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<DocumentDto> UploadAsync(Guid userId, IFormFile file, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException($"Формат {extension} не поддерживается. Допустимые форматы: .doc, .docx, .pdf");

        var fileName = $"{Guid.NewGuid()}{extension}";
        await using var stream = file.OpenReadStream();
        var s3Key = await _storage.UploadFileAsync(stream, fileName, file.ContentType, ct);

        var document = new Document
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            OriginalFileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            S3Key = s3Key,
            Format = ExtensionToFormat[extension],
            UserId = userId
        };

        _db.Documents.Add(document);
        await _db.SaveChangesAsync(ct);

        return ToDto(document);
    }

    public async Task<List<DocumentDto>> GetUserDocumentsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.Documents
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new DocumentDto(d.Id, d.OriginalFileName, d.ContentType, d.FileSize, d.Format, d.UploadedAt))
            .ToListAsync(ct);
    }

    public async Task<(Stream Stream, string FileName, string ContentType)> DownloadAsync(Guid userId, Guid documentId, CancellationToken ct = default)
    {
        var document = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Документ не найден.");

        var stream = await _storage.DownloadFileAsync(document.S3Key, ct);
        return (stream, document.OriginalFileName, document.ContentType);
    }

    public async Task DeleteAsync(Guid userId, Guid documentId, CancellationToken ct = default)
    {
        var document = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Документ не найден.");

        await _storage.DeleteFileAsync(document.S3Key, ct);
        _db.Documents.Remove(document);
        await _db.SaveChangesAsync(ct);
    }

    private static DocumentDto ToDto(Document d) =>
        new(d.Id, d.OriginalFileName, d.ContentType, d.FileSize, d.Format, d.UploadedAt);
}
