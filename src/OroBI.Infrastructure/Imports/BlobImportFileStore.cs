using System.Security.Cryptography;
using OroBI.Application.Abstractions;

namespace OroBI.Infrastructure.Imports;

public sealed class BlobImportFileStore(IBlobImportUploader uploader, TimeProvider timeProvider) : IImportFileStore
{
    public async Task<StoredImportFile> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            throw new ArgumentException("A file name is required.", nameof(fileName));
        }

        await using var bufferedContent = new MemoryStream();
        await content.CopyToAsync(bufferedContent, cancellationToken);
        var bytes = bufferedContent.ToArray();
        var hash = SHA256.HashData(bytes);
        bufferedContent.Position = 0;

        var now = timeProvider.GetUtcNow();
        var blobName = $"{now:yyyy/MM}/{Guid.NewGuid():N}-{safeFileName}";
        var uri = await uploader.UploadAsync(blobName, bufferedContent, contentType, cancellationToken);
        return new StoredImportFile(uri.ToString(), Convert.ToHexStringLower(hash));
    }
}
