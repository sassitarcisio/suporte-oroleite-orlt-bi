namespace OroBI.Application.Closings;

public static class StandardClosingCalculator
{
    public static StandardClosingSummary Calculate(StandardClosingInput input)
    {
        var ppp = PppCalculator.Calculate(input.PppMaximumAward, input.PppSegments);
        var brandAwards = input.Brands.Select(brand =>
        {
            var positivityPercent = Percentage(brand.PositivityActual, brand.PositivityGoal);
            var revenuePercent = Percentage(brand.RevenueActual, brand.RevenueGoal);
            var positivityAward = GoalPayoutCalculator.Positivity(positivityPercent, brand.PositivityPrize);
            var revenueAward = GoalPayoutCalculator.Revenue(revenuePercent, brand.RevenuePrize);
            var tradeAward = GoalPayoutCalculator.Trade(brand.TradeActualPercent, brand.TradeGoalPercent, brand.TradePrize);
            return new ClosingBrandAward(brand.Brand, positivityAward, revenueAward, tradeAward)
            {
                RevenueGoal = brand.RevenueGoal,
                RevenueActual = brand.RevenueActual,
                RevenueAchievedPercent = revenuePercent,
                RevenuePrize = brand.RevenuePrize,
                PositivityGoal = brand.PositivityGoal,
                PositivityActual = brand.PositivityActual,
                PositivityAchievedPercent = positivityPercent,
                PositivityPrize = brand.PositivityPrize,
                TradeValue = brand.TradeValue,
                TradeActualPercent = brand.TradeActualPercent,
                TradeGoalPercent = brand.TradeGoalPercent,
                TradePrize = brand.TradePrize
            };
        }).ToArray();
        var compensation = CompensationCalculator.Calculate(input.BaseSalary, input.CommissionPercent, input.CommissionableRevenue);
        return new StandardClosingSummary(ppp, brandAwards, compensation, ppp.Award + brandAwards.Sum(item => item.TotalAward));
    }

    private static decimal Percentage(decimal actual, decimal goal) => goal == 0m ? 0m : actual / goal * 100m;
}

public sealed record StandardClosingInput(
    decimal CommissionableRevenue,
    decimal BaseSalary,
    decimal CommissionPercent,
    decimal PppMaximumAward,
    IReadOnlyCollection<(decimal CustomerCount, decimal ItemsPerSegment, decimal GroupsPlaced)> PppSegments,
    IReadOnlyCollection<ClosingBrandInput> Brands);

public sealed record ClosingBrandInput(
    string Brand,
    decimal PositivityGoal,
    decimal PositivityActual,
    decimal RevenueGoal,
    decimal RevenueActual,
    decimal TradeActualPercent,
    decimal PositivityPrize,
    decimal RevenuePrize,
    decimal TradePrize,
    decimal TradeGoalPercent)
{
    public decimal TradeValue { get; init; }
}

public sealed record ClosingBrandAward(string Brand, decimal PositivityAward, decimal RevenueAward, decimal TradeAward)
{
    public decimal RevenueGoal { get; init; }
    public decimal RevenueActual { get; init; }
    public decimal RevenueAchievedPercent { get; init; }
    public decimal RevenuePrize { get; init; }
    public decimal PositivityGoal { get; init; }
    public decimal PositivityActual { get; init; }
    public decimal PositivityAchievedPercent { get; init; }
    public decimal PositivityPrize { get; init; }
    public decimal TradeValue { get; init; }
    public decimal TradeActualPercent { get; init; }
    public decimal TradeGoalPercent { get; init; }
    public decimal TradePrize { get; init; }
    public decimal TotalAward => PositivityAward + RevenueAward + TradeAward;
}

public sealed record StandardClosingSummary(
    PppSummary Ppp,
    IReadOnlyCollection<ClosingBrandAward> BrandAwards,
    CompensationSummary Compensation,
    decimal TotalAwards);
