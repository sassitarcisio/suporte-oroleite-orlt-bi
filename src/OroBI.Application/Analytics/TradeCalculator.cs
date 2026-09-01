using OroBI.Domain.Commercial;

namespace OroBI.Application.Analytics;

public static class TradeCalculator
{
    private static readonly HashSet<string> PhysicalTradeTypes = new(StringComparer.Ordinal)
    {
        "TROCA",
        "TROCA DEV"
    };

    public static TradeSummary Calculate(IEnumerable<CommercialMovement> movements)
    {
        var rows = movements.ToArray();
        var tradeRows = rows.Where(movement => PhysicalTradeTypes.Contains(movement.MovementType)).ToArray();
        var physicalTrades = tradeRows.Sum(movement => decimal.Abs(movement.TotalValue));
        var grossSales = rows.Where(movement => movement.MovementType == "VENDA").Sum(movement => movement.TotalValue);
        var tradeToSalesPercent = grossSales == 0m ? 0m : physicalTrades / grossSales * 100m;

        return new TradeSummary(physicalTrades, tradeToSalesPercent, tradeRows.Length);
    }
}
