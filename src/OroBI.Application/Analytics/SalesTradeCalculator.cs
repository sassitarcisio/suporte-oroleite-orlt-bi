using OroBI.Domain.Commercial;

namespace OroBI.Application.Analytics;

public static class SalesTradeCalculator
{
    private static readonly HashSet<string> RevenueTypes = new(StringComparer.Ordinal)
    {
        "VENDA",
        "DEVOL ENT",
        "DEVOLUCAO"
    };

    private static readonly HashSet<string> TradeTypes = new(StringComparer.Ordinal)
    {
        "TROCA",
        "TROCA DEV"
    };

    public static SalesTradeSummary Calculate(IEnumerable<CommercialMovement> movements)
    {
        var rows = movements.ToArray();
        var revenue = rows.Where(movement => RevenueTypes.Contains(movement.MovementType)).Sum(movement => movement.TotalValue);
        var trades = rows.Where(movement => TradeTypes.Contains(movement.MovementType)).Sum(movement => decimal.Abs(movement.TotalValue));
        var tradeToRevenuePercent = revenue == 0m ? 0m : trades / revenue * 100m;

        return new SalesTradeSummary(revenue, trades, tradeToRevenuePercent);
    }
}
