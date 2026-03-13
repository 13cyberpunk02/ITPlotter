namespace ITPlotter.Domain.Interfaces;

public interface IStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default);
    Task<Stream> DownloadFileAsync(string key, CancellationToken ct = default);
    Task DeleteFileAsync(string key, CancellationToken ct = default);
}
