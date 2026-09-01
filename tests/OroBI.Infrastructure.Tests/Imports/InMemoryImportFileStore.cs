using System.Security.Cryptography;
using OroBI.Application.Abstractions;

namespace OroBI.Infrastructure.Tests.Imports;

internal sealed class InMemoryImportFileStore : IImportFileStore
{
    public async Task<StoredImportFile> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken)
    {
        await using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        return new StoredImportFile($"memory://{fileName}", Convert.ToHexStringLower(SHA256.HashData(copy.ToArray())));
    }
}
