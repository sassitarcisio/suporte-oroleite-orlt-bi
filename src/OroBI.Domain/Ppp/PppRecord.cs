namespace OroBI.Domain.Ppp;

public sealed class PppRecord
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ImportBatchId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public string Seller { get; private set; } = string.Empty;
    public string Segment { get; private set; } = string.Empty;
    public int CustomerCount { get; private set; }
    public int ItemsPerSegment { get; private set; }
    public int GroupsPlaced { get; private set; }

    public static PppRecord Create(
        Guid importBatchId,
        int year,
        int month,
        string seller,
        string segment,
        int customerCount,
        int itemsPerSegment,
        int groupsPlaced) => new()
        {
            ImportBatchId = importBatchId,
            Year = year,
            Month = month,
            Seller = seller,
            Segment = segment,
            CustomerCount = customerCount,
            ItemsPerSegment = itemsPerSegment,
            GroupsPlaced = groupsPlaced
        };
}
