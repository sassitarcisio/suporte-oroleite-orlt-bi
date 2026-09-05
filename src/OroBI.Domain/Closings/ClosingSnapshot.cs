namespace OroBI.Domain.Closings;

public enum ClosingApprovalStatus { EmConferencia, Aprovado }

public sealed class ClosingSnapshot
{
    private ClosingSnapshot() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SellerId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public ClosingApprovalStatus Status { get; private set; }
    public string? SnapshotJson { get; private set; }
    public string ReviewedBy { get; private set; } = string.Empty;
    public DateTimeOffset ReviewedAtUtc { get; private set; }
    public string? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public Guid Revision { get; private set; } = Guid.NewGuid();

    public static ClosingSnapshot Review(Guid sellerId, int year, int month, string actor)
    {
        _ = new DateOnly(year, month, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        if (sellerId == Guid.Empty) throw new ArgumentException("Vendedor obrigatório.", nameof(sellerId));
        return new() { SellerId = sellerId, Year = year, Month = month,
            Status = ClosingApprovalStatus.EmConferencia, ReviewedBy = actor, ReviewedAtUtc = DateTimeOffset.UtcNow };
    }

    public void Approve(string snapshotJson, string actor)
    {
        if (Status == ClosingApprovalStatus.Aprovado) throw new InvalidOperationException("O fechamento aprovado é imutável.");
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        SnapshotJson = snapshotJson;
        ApprovedBy = actor;
        ApprovedAtUtc = DateTimeOffset.UtcNow;
        Status = ClosingApprovalStatus.Aprovado;
        Revision = Guid.NewGuid();
    }
}
