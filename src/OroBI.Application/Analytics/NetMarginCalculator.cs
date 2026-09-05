using OroBI.Domain.Commercial;

namespace OroBI.Application.Analytics;

public static class NetMarginCalculator
{
    public static NetMarginReport Calculate(IEnumerable<CommercialMovement> movements)
    {
        var relevant = movements.Where(movement => movement.MovementType is
            "VENDA" or "DEVOLUCAO" or "DEVOL ENT" or "TROCA" or "TROCA DEV" or "DESC BOLETO").ToArray();
        var totals = Aggregate(string.Empty, relevant);
        var groups = new Dictionary<string, IReadOnlyList<NetMarginRow>>
        {
            ["seller"] = Group(relevant, movement => movement.Seller),
            ["brand"] = Group(relevant, movement => movement.Brand),
            ["customer"] = Group(relevant, movement => movement.CustomerName),
            ["group"] = Group(relevant, movement => movement.Group),
            ["product"] = Group(relevant, movement => movement.ProductName, "SEM PRODUTO INFORMADO"),
            ["city"] = Group(relevant, movement => movement.City)
        };
        return NetMarginReport.Create(totals.GrossSales, totals.Returns, totals.NetCost, totals.TradeLosses,
            totals.BoletoDiscounts, groups["product"].Count) with
        {
            OwnReturns = totals.OwnReturns,
            CustomerReturns = totals.CustomerReturns,
            Quantity = totals.Quantity,
            MovementCount = totals.MovementCount,
            Groups = groups
        };
    }

    private static IReadOnlyList<NetMarginRow> Group(IEnumerable<CommercialMovement> movements,
        Func<CommercialMovement, string> selector, string missingLabel = "SEM INFORMAÇÃO") =>
        movements.GroupBy(movement => string.IsNullOrWhiteSpace(selector(movement)) ? missingLabel : selector(movement).Trim(),
            StringComparer.Ordinal).Select(group => Aggregate(group.Key, group)).ToArray();

    private static NetMarginRow Aggregate(string label, IEnumerable<CommercialMovement> movements)
    {
        decimal grossSales = 0m, salesCost = 0m, ownReturns = 0m, customerReturns = 0m,
            returnCost = 0m, tradeLosses = 0m, boletoDiscounts = 0m, quantity = 0m;
        var movementCount = 0;
        foreach (var movement in movements)
        {
            var absoluteQuantity = decimal.Abs(movement.Quantity);
            var cost = absoluteQuantity * movement.UnitCost;
            quantity += absoluteQuantity;
            movementCount++;
            switch (movement.MovementType)
            {
                case "VENDA":
                    grossSales += movement.TotalValue;
                    salesCost += cost;
                    break;
                case "DEVOLUCAO":
                    ownReturns += decimal.Abs(movement.TotalValue);
                    returnCost += cost;
                    break;
                case "DEVOL ENT":
                    customerReturns += decimal.Abs(movement.TotalValue);
                    returnCost += cost;
                    break;
                case "TROCA":
                case "TROCA DEV":
                    tradeLosses += cost;
                    break;
                case "DESC BOLETO":
                    boletoDiscounts += decimal.Abs(movement.TotalValue);
                    break;
            }
        }

        var returns = ownReturns + customerReturns;
        var netSales = grossSales - returns;
        var netCost = salesCost - returnCost;
        var losses = tradeLosses + boletoDiscounts;
        var profit = netSales - netCost - losses;
        return new NetMarginRow(label, grossSales, ownReturns, customerReturns, returns, netSales, netCost,
            tradeLosses, boletoDiscounts, profit, netSales == 0m ? null : profit / netSales * 100m,
            quantity, movementCount, losses);
    }
}
