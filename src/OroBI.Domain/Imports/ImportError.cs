namespace OroBI.Domain.Imports;

public sealed class ImportError
{
    private ImportError(Guid importBatchId, int lineNumber, string message)
    {
        ImportBatchId = importBatchId;
        LineNumber = lineNumber;
        Message = message;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ImportBatchId { get; private set; }
    public int LineNumber { get; private set; }
    public string Message { get; private set; } = string.Empty;

    public static ImportError Create(Guid importBatchId, int lineNumber, string message) =>
        new(importBatchId, lineNumber, message);
}
