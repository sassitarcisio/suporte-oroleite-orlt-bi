namespace OroBI.Application.Abstractions;

public interface IImportFileStore
{
    Task<StoredImportFile> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken);
}

public sealed record StoredImportFile(string Uri, string Sha256);
