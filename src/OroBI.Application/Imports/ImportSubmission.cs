using OroBI.Domain.Imports;

namespace OroBI.Application.Imports;

public sealed record ImportSubmission(ImportFileType FileType, string FileName, string ContentType, Stream Content);

public sealed record ImportExecutionResult(ImportBatchStatus Status, string? StoredFileUri, int ProcessedRows, int ErrorRows);
