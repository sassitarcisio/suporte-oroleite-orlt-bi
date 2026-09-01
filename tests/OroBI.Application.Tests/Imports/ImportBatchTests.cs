using OroBI.Domain.Imports;

namespace OroBI.Application.Tests.Imports;

public sealed class ImportBatchTests
{
    [Fact]
    public void Completes_batch_with_persisted_file_and_row_totals()
    {
        var batch = ImportBatch.Start(ImportFileType.Power, "power.csv", "abc");

        batch.Complete("imports/power/abc.csv", processedRows: 10, errorRows: 2);

        Assert.Equal(ImportBatchStatus.CompletedWithErrors, batch.Status);
        Assert.Equal("imports/power/abc.csv", batch.StoredFileUri);
        Assert.Equal(10, batch.ProcessedRows);
        Assert.Equal(2, batch.ErrorRows);
    }
}
