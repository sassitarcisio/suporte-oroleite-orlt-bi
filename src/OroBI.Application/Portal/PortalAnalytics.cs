namespace OroBI.Application.Portal;

public sealed record PortalDataFreshness(string Source, DateTimeOffset? UpdatedAtUtc, string TimestampKind);
public sealed record PortalRevenueSummary(decimal GrossSales, decimal NetRevenue, decimal NegativeMovements,
    decimal SaleQuantity, int MovementCount, int? CustomerCount, int DocumentCount)
{
    public decimal? AverageTicket { get; init; }
}
public sealed record PortalDailyRevenue(DateOnly Date, decimal GrossSales, decimal NetRevenue, decimal NegativeMovements);
public sealed record PortalDashboard(DateOnly StartDate, DateOnly EndDate, DateOnly ReferenceDate,
    PortalRevenueSummary Period, PortalRevenueSummary Month, PortalRevenueSummary Today,
    IReadOnlyList<PortalDailyRevenue> DailyTrend, PortalDataFreshness Freshness);
public sealed record PortalPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
public sealed record PortalSale(Guid Id, DateOnly Date, string DocumentNumber, string MovementType,
    string CustomerCode, string CustomerName, string ProductName, string Brand, decimal Quantity, decimal TotalValue);
public sealed record PortalCustomer(string CustomerCode, string CustomerName, string City, decimal GrossSales,
    decimal NetRevenue, int DocumentCount, DateOnly LastPurchaseDate)
{
    public decimal? AverageTicket { get; init; }
    public decimal PurchasedQuantity { get; init; }
}
public sealed record PortalCustomers(bool ObservedBuyersOnly, IReadOnlyList<PortalCustomer> Items, int TotalCount, bool HasMore);
public sealed record PortalCustomerDetail(PortalCustomer Customer, IReadOnlyList<PortalSale> Sales, int TotalCount, bool HasMore);
public sealed record PortalRankingItem(string Label, decimal GrossSales, decimal NetRevenue, decimal Quantity,
    int MovementCount, int? CustomerCount, decimal? RevenueSharePercent);
public sealed record PortalRanking(IReadOnlyList<PortalRankingItem> Items, int TotalCount, bool HasMore);
public sealed record PortalGoal(string Brand, string Type, decimal Target, decimal Actual, decimal? AchievedPercent,
    decimal? MaximumPrize, decimal? CurrentPrize, decimal? NextTierPercent, decimal? AmountToNextTier, decimal? NextTierPrize);
public sealed record PortalGoals(int Year, int Month, bool Available, string? UnavailableReason, IReadOnlyList<PortalGoal> Items)
{
    public bool IsApproved { get; init; }
}
public sealed record PortalPppSegment(string Segment, int? CustomerCount, int ItemsPerSegment, int GroupsPlaced, decimal? AchievementPercent);
public sealed record PortalPpp(int Year, int Month, bool Available, string? UnavailableReason, decimal? AchievementPercent,
    decimal? Award, IReadOnlyList<PortalPppSegment> Segments)
{
    public bool IsApproved { get; init; }
}
public sealed record PortalTrades(decimal PhysicalTrades, decimal TradeToSalesPercent, int MovementCount,
    IReadOnlyList<PortalSale> Items, bool HasMore);
