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
        var customerCount = rows.Select(movement => string.IsNullOrWhiteSpace(movement.CustomerCode) ? movement.CustomerName : movement.CustomerCode)
            .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var documentCount = rows.Select(movement => movement.DocumentNumber).Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase).Count();

        return new DashboardSummary(grossSales, negativeMovements, negativePercent, netResult, saleQuantity, rows.Length, customerCount, documentCount);
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

        DashboardGroupRow[] Aggregate(Func<CommercialMovement, string> key) => rows
            .GroupBy(row => string.IsNullOrWhiteSpace(key(row)) ? "SEM INFORMAÇÃO" : key(row).Trim())
            .Select(group => new DashboardGroupRow(group.Key,
                group.Sum(row => row.TotalValue),
                group.Where(row => row.MovementType == "VENDA").Sum(row => row.TotalValue),
                group.Where(row => row.TotalValue < 0m).Sum(row => decimal.Abs(row.TotalValue)),
                group.Sum(row => row.Quantity), group.Count(),
                group.Select(row => row.DocumentNumber).Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count()))
            .OrderByDescending(row => row.NetResult).ThenBy(row => row.Label, StringComparer.Ordinal).ToArray();

        return new DashboardDetails(dailyTrend, sellerResults)
        {
            Groups = new()
            {
                ["seller"] = Aggregate(row => row.Seller),
                ["brand"] = Aggregate(row => row.Brand),
                ["customer"] = Aggregate(row => row.CustomerName),
                ["group"] = Aggregate(row => row.Group),
                ["product"] = Aggregate(row => row.ProductName),
                ["city"] = Aggregate(row => row.City),
                ["movementType"] = Aggregate(row => row.MovementType),
                ["family"] = Aggregate(row => row.Family),
                ["date"] = Aggregate(row => row.MovementDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))
            }
        };
    }
}
