namespace OroBI.Application.Closings;

public sealed record ClosingMonthlySummary(
    string Scope,
    decimal Revenue,
    decimal CommissionableRevenue,
    decimal TradeValue,
    decimal TradePercent,
    int MovementCount,
    int CustomerCount,
    IReadOnlyCollection<ClosingDocument> Documents)
{
    public int DocumentCount => Documents.Count;
}

public sealed record ClosingDocument(
    string DocumentNumber,
    DateOnly Date,
    string Seller,
    string CustomerCode,
    string CustomerName,
    string MovementType,
    decimal TotalValue);

public sealed record ClosingPppSegment(
    string Segment,
    int CustomerCount,
    int ItemsPerSegment,
    int GroupsPlaced)
{
    public decimal? AchievementPercent => CustomerCount > 0 && ItemsPerSegment > 0
        ? GroupsPlaced / ((decimal)CustomerCount * ItemsPerSegment) * 100m
        : null;
}
