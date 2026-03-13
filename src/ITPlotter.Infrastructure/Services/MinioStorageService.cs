using ITPlotter.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;

namespace ITPlotter.Infrastructure.Services;

public class MinioStorageService : IStorageService
{
    private readonly IMinioClient _minio;
    private readonly string _bucketName;

    public MinioStorageService(IMinioClient minio, IConfiguration configuration)
    {
        _minio = minio;
        _bucketName = configuration["Minio:BucketName"] ?? "documents";
    }

    public async Task EnsureBucketExistsAsync()
    {
        var exists = await _minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucketName));
        if (!exists)
        {
            await _minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucketName));
        }
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        var key = $"{DateTime.UtcNow:yyyy/MM/dd}/{fileName}";

        await _minio.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(key)
            .WithStreamData(fileStream)
            .WithObjectSize(fileStream.Length)
            .WithContentType(contentType), ct);

        return key;
    }

    public async Task<Stream> DownloadFileAsync(string key, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        await _minio.GetObjectAsync(new GetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(key)
            .WithCallbackStream(stream => stream.CopyTo(ms)), ct);
        ms.Position = 0;
        return ms;
    }

    public async Task DeleteFileAsync(string key, CancellationToken ct = default)
    {
        await _minio.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(key), ct);
    }
}
