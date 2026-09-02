namespace OroBI.Infrastructure.Imports;

public interface IBlobImportUploader
{
    Task<Uri> UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken);
}
