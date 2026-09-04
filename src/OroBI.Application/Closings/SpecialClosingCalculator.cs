namespace OroBI.Application.Closings;

public static class SpecialClosingCalculator
{
    public static SpecialClosingSummary CalculateDeivid(DeividClosingInput input)
    {
        var commission = input.OwnCommissionableRevenue * 0.01m +
            input.TeamCommissionableRevenue * 0.0015m +
            input.NetworkCommissionableRevenue * 0.0015m;
        var tradeAward = DeividTradeAward(input.TradePercent);
        return new SpecialClosingSummary(input.BaseSalary, commission, input.TeamAward, tradeAward);
    }

    public static SpecialClosingSummary CalculateValdir(ValdirClosingInput input) =>
        new(input.BaseSalary, input.CompanyCommissionableRevenue * 0.001m, 0m, ValdirTradeAward(input.TradePercent));

    public static decimal DeividTradeAward(decimal tradePercent) => tradePercent switch
    {
        <= 1.25m => 5000m,
        <= 1.75m => 3000m,
        <= 2.25m => 2000m,
        _ => 0m
    };

    public static decimal ValdirTradeAward(decimal tradePercent) => tradePercent switch
    {
        <= 2m => 5000m,
        <= 3m => 3000m,
        <= 4m => 2000m,
        _ => 0m
    };
}

public sealed record DeividClosingInput(
    decimal BaseSalary,
    decimal OwnCommissionableRevenue,
    decimal TeamCommissionableRevenue,
    decimal NetworkCommissionableRevenue,
    decimal TeamAward,
    decimal TradePercent);

public sealed record ValdirClosingInput(
    decimal BaseSalary,
    decimal CompanyCommissionableRevenue,
    decimal TradePercent);

public sealed record SpecialClosingSummary(decimal BaseSalary, decimal Commission, decimal TeamAward, decimal TradeAward)
{
    public decimal SalaryAndCommission => BaseSalary + Commission;
    public decimal TotalAwards => TeamAward + TradeAward;
    public decimal Total => SalaryAndCommission + TotalAwards;
}
