namespace OroBI.Application.Imports;

public interface IImportWorkflow
{
    Task<ImportExecutionResult> ImportAsync(ImportSubmission submission, CancellationToken cancellationToken);
}
