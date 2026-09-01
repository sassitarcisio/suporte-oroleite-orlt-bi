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

        return new MarginSummary(revenue, cost, grossProfit, marginPercent);
    }
}
