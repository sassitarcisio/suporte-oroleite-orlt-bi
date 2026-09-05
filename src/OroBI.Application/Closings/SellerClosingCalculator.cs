namespace OroBI.Application.Closings;

public static class SellerClosingCalculator
{
    public static SellerClosingSummary Calculate(SellerClosingInput input)
    {
        var ppp = PppCalculator.Calculate(input.PppMaximumAward, input.PppSegments);
        var revenueAward = GoalPayoutCalculator.Revenue(input.RevenueAchievedPercent, input.RevenuePrize);
        var positivityAward = GoalPayoutCalculator.Positivity(input.PositivityAchievedPercent, input.PositivityPrize);
        var tradeAward = GoalPayoutCalculator.Trade(input.TradeActualPercent, input.TradeGoalPercent, input.TradePrize);
        var compensation = CompensationCalculator.Calculate(input.BaseSalary, input.CommissionPercent, input.Revenue);
        return new SellerClosingSummary(ppp, revenueAward, positivityAward, tradeAward, compensation, ppp.Award + revenueAward + positivityAward + tradeAward);
    }
}

public sealed record SellerClosingInput(
    decimal Revenue,
    decimal BaseSalary,
    decimal CommissionPercent,
    decimal RevenueAchievedPercent,
    decimal RevenuePrize,
    decimal PositivityAchievedPercent,
    decimal PositivityPrize,
    decimal TradeActualPercent,
    decimal TradeGoalPercent,
    decimal TradePrize,
    decimal PppMaximumAward,
    IReadOnlyCollection<(decimal CustomerCount, decimal ItemsPerSegment, decimal GroupsPlaced)> PppSegments);

public sealed record SellerClosingSummary(
    PppSummary Ppp,
    decimal RevenueAward,
    decimal PositivityAward,
    decimal TradeAward,
    CompensationSummary Compensation,
    decimal TotalAwards)
{
    public bool IsApproved { get; init; }
    public IReadOnlyCollection<ClosingBrandAward> BrandAwards { get; init; } = [];
    public ClosingMonthlySummary Monthly { get; init; } = new("seller", 0m, 0m, 0m, 0m, 0, 0, []);
    public IReadOnlyCollection<ClosingPppSegment> PppSegments { get; init; } = [];
    public SupervisorClosingDetails? Supervisor { get; init; }
    public decimal? CommissionPercent { get; init; }
    public decimal Total => Compensation.TotalSalary + TotalAwards;
}
