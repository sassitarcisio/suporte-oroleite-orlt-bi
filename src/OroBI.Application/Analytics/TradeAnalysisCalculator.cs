using OroBI.Domain.Commercial;

namespace OroBI.Application.Analytics;

public static class TradeAnalysisCalculator
{
    private static readonly HashSet<string> TradeTypes = new(StringComparer.Ordinal) { "TROCA", "TROCA DEV" };
    private static readonly HashSet<string> RevenueTypes = new(StringComparer.Ordinal) { "VENDA", "DEVOL ENT", "DEVOLUCAO" };

    public static TradeAnalysisReport Calculate(IEnumerable<CommercialMovement> movements)
    {
        var rows = movements.ToArray();
        var tradeRows = rows.Where(item => TradeTypes.Contains(item.MovementType)).ToArray();
        var grossSales = rows.Where(item => item.MovementType == "VENDA").Sum(item => item.TotalValue);
        var netRevenue = rows.Where(item => RevenueTypes.Contains(item.MovementType)).Sum(item => item.TotalValue);
        var totalTradeValue = tradeRows.Sum(item => decimal.Abs(item.TotalValue));

        return new TradeAnalysisReport(
            rows.Length,
            grossSales,
            netRevenue,
            totalTradeValue,
            netRevenue == 0m ? 0m : totalTradeValue / netRevenue * 100m,
            ValueFor(tradeRows, "TROCA DEV"),
            ValueFor(tradeRows, "TROCA"),
            tradeRows.Sum(item => decimal.Abs(item.Quantity)),
            tradeRows.Length,
            CountDistinct(tradeRows, item => item.CustomerCode, item => item.CustomerName),
            CountDistinct(tradeRows, item => item.ProductName),
            CountDistinct(tradeRows, item => item.Brand),
            tradeRows.GroupBy(item => item.MovementDate).OrderBy(group => group.Key).Select(group => new DailyTradeValue(group.Key, group.Sum(item => decimal.Abs(item.TotalValue)))).ToArray(),
            Rank(tradeRows, item => item.Seller),
            Rank(tradeRows, item => DisplayCustomer(item)),
            Rank(tradeRows, item => item.ProductName),
            Rank(tradeRows, item => item.Brand));
    }

    private static decimal ValueFor(IEnumerable<CommercialMovement> movements, string type) =>
        movements.Where(item => item.MovementType == type).Sum(item => decimal.Abs(item.TotalValue));

    private static int CountDistinct(IEnumerable<CommercialMovement> movements, params Func<CommercialMovement, string>[] selectors) =>
        movements.Select(item => selectors.Select(selector => selector(item)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).Count();

    private static string DisplayCustomer(CommercialMovement movement) =>
        !string.IsNullOrWhiteSpace(movement.CustomerName) ? movement.CustomerName : movement.CustomerCode;

    private static IReadOnlyList<TradeRankItem> Rank(IEnumerable<CommercialMovement> movements, Func<CommercialMovement, string> selector) =>
        movements.Select(item => new { Name = selector(item), Value = decimal.Abs(item.TotalValue) })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => item.Name, StringComparer.Ordinal)
            .Select(group => new TradeRankItem(group.Key, group.Sum(item => item.Value)))
            .OrderByDescending(item => item.Value).ThenBy(item => item.Name, StringComparer.Ordinal).Take(10).ToArray();
}
