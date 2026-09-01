using OroBI.Infrastructure.Imports;

namespace OroBI.Infrastructure.Tests.Imports;

public sealed class LocalImportFileStoreTests
{
    [Fact]
    public async Task Stores_file_and_returns_its_sha256_checksum()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"orobi-tests-{Guid.NewGuid():N}");
        try
        {
            var store = new LocalImportFileStore(rootPath);
            await using var content = new MemoryStream("abc"u8.ToArray());

            var storedFile = await store.SaveAsync(content, "power.csv", "text/csv", CancellationToken.None);

            Assert.True(File.Exists(storedFile.Uri));
            Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", storedFile.Sha256);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }
}
