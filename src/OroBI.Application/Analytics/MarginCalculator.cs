using OroBI.Domain.Commercial;

namespace OroBI.Application.Analytics;

public static class MarginCalculator
{
    public static MarginSummary Calculate(IEnumerable<CommercialMovement> movements)
    {
        var sales = movements.Where(movement => movement.MovementType == "VENDA").ToArray();
        var revenue = sales.Sum(movement => movement.TotalValue);
        var cost = sales.Sum(movement => movement.Quantity * movement.UnitCost);
        var grossProfit = revenue - cost;
        var marginPercent = revenue == 0m ? 0m : grossProfit / revenue * 100m;

        var groups = new Dictionary<string, IReadOnlyList<MarginRow>>
        {
            ["customer"] = Group(sales, movement => movement.CustomerName),
            ["product"] = Group(sales, movement => movement.ProductName),
            ["brand"] = Group(sales, movement => movement.Brand)
        };
        return new MarginSummary(revenue, cost, grossProfit, marginPercent)
        {
            CustomerCount = sales.Select(movement => string.IsNullOrWhiteSpace(movement.CustomerCode)
                ? Label(movement.CustomerName) : movement.CustomerCode.Trim()).Distinct(StringComparer.Ordinal).Count(),
            ProductCount = groups["product"].Count,
            MovementCount = sales.Length,
            Groups = groups
        };
    }

    private static IReadOnlyList<MarginRow> Group(IEnumerable<CommercialMovement> sales, Func<CommercialMovement, string> selector) =>
        sales.GroupBy(movement => Label(selector(movement)), StringComparer.Ordinal).Select(group =>
        {
            var revenue = group.Sum(movement => movement.TotalValue);
            var cost = group.Sum(movement => movement.Quantity * movement.UnitCost);
            var profit = revenue - cost;
            return new MarginRow(group.Key, revenue, cost, profit, revenue == 0m ? null : profit / revenue * 100m,
                group.Sum(movement => movement.Quantity));
        }).ToArray();

    private static string Label(string value) => string.IsNullOrWhiteSpace(value) ? "SEM INFORMAÇÃO" : value.Trim();
}
