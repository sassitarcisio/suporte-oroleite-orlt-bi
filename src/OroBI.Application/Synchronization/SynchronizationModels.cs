namespace OroBI.Application.Synchronization;

public sealed record FirebirdCommercialRecord(
    string SourceRecordKey,
    DateOnly MovementDate,
    string Seller,
    string MovementType,
    decimal TotalValue,
    decimal Quantity);

public sealed record SynchronizationPage(
    string? NextWatermark,
    IReadOnlyCollection<FirebirdCommercialRecord> Records);
