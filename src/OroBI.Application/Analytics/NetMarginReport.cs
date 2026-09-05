namespace OroBI.Application.Analytics;

public sealed record NetMarginReport(
    decimal GrossSales,
    decimal Returns,
    decimal NetSales,
    decimal NetCost,
    decimal TradeLosses,
    decimal BoletoDiscounts,
    decimal LiquidProfit,
    decimal LiquidMarginPercent,
    int ProductCount)
{
    public decimal OwnReturns { get; init; }
    public decimal CustomerReturns { get; init; }
    public decimal Quantity { get; init; }
    public int MovementCount { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyList<NetMarginRow>> Groups { get; init; } =
        new Dictionary<string, IReadOnlyList<NetMarginRow>>
        {
            ["seller"] = [], ["brand"] = [], ["customer"] = [],
            ["group"] = [], ["product"] = [], ["city"] = []
        };

    public static NetMarginReport Create(
        decimal grossSales,
        decimal returns,
        decimal netCost,
        decimal tradeLosses,
        decimal boletoDiscounts,
        int productCount)
    {
        var netSales = grossSales - returns;
        var liquidProfit = netSales - netCost - tradeLosses - boletoDiscounts;
        var liquidMarginPercent = netSales == 0m ? 0m : liquidProfit / netSales * 100m;
        return new NetMarginReport(grossSales, returns, netSales, netCost, tradeLosses, boletoDiscounts, liquidProfit, liquidMarginPercent, productCount);
    }
}

public sealed record NetMarginRow(
    string Label,
    decimal GrossSales,
    decimal OwnReturns,
    decimal CustomerReturns,
    decimal Returns,
    decimal NetSales,
    decimal NetCost,
    decimal TradeLosses,
    decimal BoletoDiscounts,
    decimal LiquidProfit,
    decimal? LiquidMarginPercent,
    decimal Quantity,
    int MovementCount,
    decimal Losses);
