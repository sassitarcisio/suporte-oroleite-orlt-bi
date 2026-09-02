using System.Text;
using OroBI.Infrastructure.Imports;

namespace OroBI.Infrastructure.Tests.Imports;

public sealed class BlobImportFileStoreTests
{
    [Fact]
    public async Task Uploads_private_blob_with_month_partition_safe_name_and_checksum()
    {
        var uploader = new RecordingBlobImportUploader(new Uri("https://orobistore.blob.core.windows.net/imports/"));
        var store = new BlobImportFileStore(uploader, new FixedTimeProvider(new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero)));
        await using var content = new MemoryStream("abc"u8.ToArray());

        var result = await store.SaveAsync(content, "../POWER.csv", "text/csv", CancellationToken.None);

        Assert.Matches("^2026/09/[a-f0-9]{32}-POWER\\.csv$", uploader.BlobName);
        Assert.Equal("text/csv", uploader.ContentType);
        Assert.Equal("abc", Encoding.UTF8.GetString(uploader.Bytes));
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", result.Sha256);
        Assert.StartsWith("https://orobistore.blob.core.windows.net/imports/2026/09/", result.Uri);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingBlobImportUploader(Uri containerUri) : IBlobImportUploader
    {
        public string BlobName { get; private set; } = string.Empty;
        public string ContentType { get; private set; } = string.Empty;
        public byte[] Bytes { get; private set; } = [];

        public async Task<Uri> UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken)
        {
            BlobName = blobName;
            ContentType = contentType;
            await using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            Bytes = copy.ToArray();
            return new Uri(containerUri, blobName);
        }
    }
}
