namespace OroBI.Domain.Imports;

public sealed class ImportBatch
{
    private ImportBatch(ImportFileType fileType, string fileName, string checksum)
    {
        Id = Guid.NewGuid();
        FileType = fileType;
        FileName = fileName;
        Checksum = checksum;
        StartedAtUtc = DateTimeOffset.UtcNow;
        Status = ImportBatchStatus.Pending;
    }

    public Guid Id { get; private set; }
    public ImportFileType FileType { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string Checksum { get; private set; } = string.Empty;
    public string? StoredFileUri { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public ImportBatchStatus Status { get; private set; }
    public int ProcessedRows { get; private set; }
    public int ErrorRows { get; private set; }

    public static ImportBatch Start(ImportFileType fileType, string fileName, string checksum) =>
        new(fileType, fileName, checksum);

    public void Complete(string storedFileUri, int processedRows, int errorRows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storedFileUri);
        ArgumentOutOfRangeException.ThrowIfNegative(processedRows);
        ArgumentOutOfRangeException.ThrowIfNegative(errorRows);

        StoredFileUri = storedFileUri;
        ProcessedRows = processedRows;
        ErrorRows = errorRows;
        Status = errorRows == 0 ? ImportBatchStatus.Completed : ImportBatchStatus.CompletedWithErrors;
    }

    public void Reject(string storedFileUri, int errorRows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storedFileUri);
        ArgumentOutOfRangeException.ThrowIfNegative(errorRows);

        StoredFileUri = storedFileUri;
        ErrorRows = errorRows;
        Status = ImportBatchStatus.Rejected;
    }
}
