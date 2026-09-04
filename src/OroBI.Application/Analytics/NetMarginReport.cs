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
