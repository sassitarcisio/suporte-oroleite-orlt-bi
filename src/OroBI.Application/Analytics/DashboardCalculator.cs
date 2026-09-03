using OroBI.Domain.Commercial;

namespace OroBI.Application.Analytics;

public static class DashboardCalculator
{
    public static DashboardSummary Calculate(IEnumerable<CommercialMovement> movements)
    {
        var rows = movements.ToArray();
        var grossSales = rows
            .Where(movement => movement.MovementType == "VENDA")
            .Sum(movement => movement.TotalValue);
        var negativeMovements = rows
            .Where(movement => movement.TotalValue < 0m)
            .Sum(movement => decimal.Abs(movement.TotalValue));
        var netResult = rows.Sum(movement => movement.TotalValue);
        var negativePercent = grossSales == 0m ? 0m : negativeMovements / grossSales * 100m;
        var saleQuantity = rows.Where(movement => movement.MovementType == "VENDA").Sum(movement => movement.Quantity);

        return new DashboardSummary(grossSales, negativeMovements, negativePercent, netResult, saleQuantity, rows.Length);
    }

    public static DashboardDetails BuildDetails(IEnumerable<CommercialMovement> movements)
    {
        var rows = movements.ToArray();
        var dailyTrend = rows.GroupBy(movement => movement.MovementDate).OrderBy(group => group.Key)
            .Select(group => new DashboardTrendPoint(group.Key,
                group.Where(movement => movement.MovementType == "VENDA").Sum(movement => movement.TotalValue),
                group.Sum(movement => movement.TotalValue),
                group.Where(movement => movement.TotalValue < 0m).Sum(movement => decimal.Abs(movement.TotalValue))))
            .ToArray();
        var sellerResults = rows.GroupBy(movement => movement.Seller)
            .Select(group => new DashboardSellerResult(group.Key, group.Sum(movement => movement.TotalValue)))
            .OrderByDescending(result => result.NetResult).ThenBy(result => result.Seller).Take(10).ToArray();

        return new DashboardDetails(dailyTrend, sellerResults);
    }
}
