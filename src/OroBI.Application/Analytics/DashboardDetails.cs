namespace OroBI.Application.Analytics;

public sealed record DashboardTrendPoint(DateOnly Date, decimal GrossSales, decimal NetResult, decimal NegativeMovements);

public sealed record DashboardSellerResult(string Seller, decimal NetResult);

public sealed record DashboardGroupRow(string Label, decimal NetResult, decimal GrossSales,
    decimal NegativeMovements, decimal Quantity, int MovementCount, int DocumentCount);

public sealed record DashboardDetails(DashboardTrendPoint[] DailyTrend, DashboardSellerResult[] SellerResults)
{
    public Dictionary<string, DashboardGroupRow[]> Groups { get; init; } = [];
}
