using System.Security.Cryptography;
using OroBI.Application.Abstractions;

namespace OroBI.Infrastructure.Imports;

public sealed class LocalImportFileStore(string rootPath) : IImportFileStore
{
    public async Task<StoredImportFile> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var safeFileName = Path.GetFileName(fileName);
        var relativeDirectory = Path.Combine(DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"));
        var targetDirectory = Path.Combine(rootPath, relativeDirectory);
        Directory.CreateDirectory(targetDirectory);

        var targetPath = Path.Combine(targetDirectory, $"{Guid.NewGuid():N}-{safeFileName}");
        await using (var target = File.Create(targetPath))
        {
            await content.CopyToAsync(target, cancellationToken);
        }

        await using var storedContent = File.OpenRead(targetPath);
        var hash = await SHA256.HashDataAsync(storedContent, cancellationToken);
        return new StoredImportFile(targetPath, Convert.ToHexStringLower(hash));
    }
}
