using OroBI.Application.Analytics;
using OroBI.Domain.Commercial;

namespace OroBI.Application.Tests.Analytics;

public sealed class NetMarginCalculatorTests
{
    [Fact]
    public void Reverses_both_return_costs_and_uses_cost_for_both_trade_types_excluding_bonuses()
    {
        var report = NetMarginCalculator.Calculate([
            Movement("VENDA", 200m, -4m, 20m),
            Movement("DEVOLUCAO", -30m, -1m, 20m),
            Movement("DEVOL ENT", 20m, 1m, 10m),
            Movement("TROCA", -99m, -2m, 3m),
            Movement("TROCA DEV", 88m, 1m, 4m),
            Movement("DESC BOLETO", -5m, -1m, 100m),
            Movement("BONIFICACAO", 900m, 90m, 90m),
            Movement("OUTRO", 800m, 80m, 80m)]);

        Assert.Equal(200m, report.GrossSales);
        Assert.Equal(30m, report.OwnReturns);
        Assert.Equal(20m, report.CustomerReturns);
        Assert.Equal(50m, report.Returns);
        Assert.Equal(150m, report.NetSales);
        Assert.Equal(50m, report.NetCost);
        Assert.Equal(10m, report.TradeLosses);
        Assert.Equal(5m, report.BoletoDiscounts);
        Assert.Equal(85m, report.LiquidProfit);
        Assert.Equal(56.667m, decimal.Round(report.LiquidMarginPercent, 3));
        Assert.Equal(10m, report.Quantity);
        Assert.Equal(6, report.MovementCount);
        Assert.Equal(1, report.ProductCount);
        var row = Assert.Single(report.Groups["product"]);
        Assert.Equal(15m, row.Losses);
        Assert.Equal(report.LiquidMarginPercent, row.LiquidMarginPercent);
    }

    [Fact]
    public void All_dimensions_reconcile_deduction_only_and_missing_product_groups_to_totals()
    {
        var report = NetMarginCalculator.Calculate([
            Movement("VENDA", 200m, 4m, 20m),
            Movement("DEVOL ENT", -30m, -1m, 20m, product: "Retorno", seller: "BRUNO"),
            Movement("TROCA", -90m, -2m, 5m, product: " ", seller: " ")]);

        Assert.Equal(3, report.ProductCount);
        Assert.Equal(6, report.Groups.Count);
        Assert.Contains(report.Groups["product"], row => row.Label == "SEM PRODUTO INFORMADO");
        Assert.Contains(report.Groups["seller"], row => row.Label == "SEM INFORMAÇÃO");
        foreach (var rows in report.Groups.Values)
        {
            Assert.Equal(report.GrossSales, rows.Sum(row => row.GrossSales));
            Assert.Equal(report.OwnReturns, rows.Sum(row => row.OwnReturns));
            Assert.Equal(report.CustomerReturns, rows.Sum(row => row.CustomerReturns));
            Assert.Equal(report.Returns, rows.Sum(row => row.Returns));
            Assert.Equal(report.NetSales, rows.Sum(row => row.NetSales));
            Assert.Equal(report.NetCost, rows.Sum(row => row.NetCost));
            Assert.Equal(report.TradeLosses, rows.Sum(row => row.TradeLosses));
            Assert.Equal(report.BoletoDiscounts, rows.Sum(row => row.BoletoDiscounts));
            Assert.Equal(report.LiquidProfit, rows.Sum(row => row.LiquidProfit));
            Assert.Equal(7m, rows.Sum(row => row.Quantity));
            Assert.Equal(3, rows.Sum(row => row.MovementCount));
            Assert.Equal(10m, rows.Sum(row => row.Losses));
        }
    }

    [Fact]
    public void Zero_net_revenue_has_null_detail_percentage_and_zero_summary_percentage()
    {
        var report = NetMarginCalculator.Calculate([
            Movement("VENDA", 100m, 2m, 20m),
            Movement("DEVOLUCAO", -100m, -1m, 20m)]);
        Assert.Equal(0m, report.NetSales);
        Assert.Equal(-20m, report.LiquidProfit);
        Assert.Equal(0m, report.LiquidMarginPercent);
        Assert.Null(Assert.Single(report.Groups["product"]).LiquidMarginPercent);
    }

    [Fact]
    public void Negative_net_revenue_keeps_signed_denominator()
    {
        var report = NetMarginCalculator.Calculate([Movement("DEVOL ENT", -100m, -2m, 20m)]);
        Assert.Equal(-100m, report.NetSales);
        Assert.Equal(-40m, report.NetCost);
        Assert.Equal(-60m, report.LiquidProfit);
        Assert.Equal(60m, report.LiquidMarginPercent);
        Assert.Equal(60m, Assert.Single(report.Groups["product"]).LiquidMarginPercent);
    }

    [Fact]
    public void No_relevant_movements_returns_empty_groups_and_zero_totals()
    {
        var report = NetMarginCalculator.Calculate([Movement("BONIFICACAO", 100m, 3m, 20m)]);
        Assert.Equal(0m, report.GrossSales);
        Assert.Equal(0m, report.LiquidProfit);
        Assert.Equal(0m, report.LiquidMarginPercent);
        Assert.Equal(0m, report.Quantity);
        Assert.Equal(0, report.MovementCount);
        Assert.Equal(0, report.ProductCount);
        Assert.All(report.Groups.Values, rows => Assert.Empty(rows));
    }

    private static CommercialMovement Movement(string type, decimal value, decimal quantity, decimal cost, string product = "Produto", string seller = "ANA") =>
        CommercialMovement.CreateFromImport(Guid.NewGuid(), new DateOnly(2026, 8, 1), seller, "MARCA", "GRUPO", type, "CIDADE", "CLIENTE", product, value, quantity, cost, "1", "1");
}
