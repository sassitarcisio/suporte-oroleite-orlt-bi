namespace OroBI.Domain.Synchronization;

public sealed class SynchronizationCheckpoint
{
    private SynchronizationCheckpoint(string sourceSystem, string watermark)
    {
        SourceSystem = sourceSystem;
        Watermark = watermark;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public string SourceSystem { get; private set; } = string.Empty;
    public string Watermark { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static SynchronizationCheckpoint Advance(string sourceSystem, string watermark)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(watermark);
        return new SynchronizationCheckpoint(sourceSystem.Trim().ToUpperInvariant(), watermark);
    }
}
