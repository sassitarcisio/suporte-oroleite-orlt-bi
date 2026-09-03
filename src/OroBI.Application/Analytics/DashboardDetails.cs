namespace OroBI.Application.Analytics;

public sealed record DashboardTrendPoint(DateOnly Date, decimal GrossSales, decimal NetResult, decimal NegativeMovements);

public sealed record DashboardSellerResult(string Seller, decimal NetResult);

public sealed record DashboardDetails(DashboardTrendPoint[] DailyTrend, DashboardSellerResult[] SellerResults);
