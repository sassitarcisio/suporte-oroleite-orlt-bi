namespace OroBI.Application.Imports;

public sealed record ImportValidationResult(ImportValidationStatus Status, IReadOnlyList<ImportValidationError> Errors)
{
    public static ImportValidationResult Valid() => new(ImportValidationStatus.Valid, []);

    public static ImportValidationResult Rejected(IReadOnlyList<ImportValidationError> errors) =>
        new(ImportValidationStatus.Rejected, errors);
}
