namespace OroBI.Application.Analytics;

public sealed record DashboardSummary(
    decimal GrossSales,
    decimal NegativeMovements,
    decimal NegativePercent,
    decimal NetResult,
    decimal SaleQuantity,
    int MovementCount);
