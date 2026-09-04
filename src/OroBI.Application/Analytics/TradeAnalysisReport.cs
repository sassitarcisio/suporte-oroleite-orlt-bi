namespace OroBI.Application.Analytics;

public sealed record TradeRankItem(string Name, decimal Value);

public sealed record DailyTradeValue(DateOnly Date, decimal Value);

public sealed record TradeAnalysisReport(
    int FilteredMovementCount,
    decimal GrossSales,
    decimal NetRevenue,
    decimal TotalTradeValue,
    decimal TradeToRevenuePercent,
    decimal TradeDevValue,
    decimal TradeValue,
    decimal TradeQuantity,
    int TradeMovementCount,
    int CustomerCount,
    int ProductCount,
    int BrandCount,
    IReadOnlyList<DailyTradeValue> DailyTrend,
    IReadOnlyList<TradeRankItem> SellerRanking,
    IReadOnlyList<TradeRankItem> CustomerRanking,
    IReadOnlyList<TradeRankItem> ProductRanking,
    IReadOnlyList<TradeRankItem> BrandRanking);
