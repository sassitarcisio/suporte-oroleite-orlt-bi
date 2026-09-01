namespace OroBI.Domain.Synchronization;

public sealed class SynchronizationRun
{
    private SynchronizationRun(string sourceSystem)
    {
        Id = Guid.NewGuid();
        SourceSystem = sourceSystem;
        StartedAtUtc = DateTimeOffset.UtcNow;
        Status = SynchronizationRunStatus.Running;
    }

    public Guid Id { get; private set; }
    public string SourceSystem { get; private set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public SynchronizationRunStatus Status { get; private set; }
    public int ProcessedRecords { get; private set; }
    public string? ErrorSummary { get; private set; }

    public static SynchronizationRun Start(string sourceSystem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSystem);
        return new SynchronizationRun(sourceSystem.Trim().ToUpperInvariant());
    }
}
