using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace OroBI.Infrastructure.Imports;

public sealed class AzureBlobImportUploader(BlobContainerClient containerClient) : IBlobImportUploader
{
    public async Task<Uri> UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, cancellationToken);
        return blobClient.Uri;
    }
}
