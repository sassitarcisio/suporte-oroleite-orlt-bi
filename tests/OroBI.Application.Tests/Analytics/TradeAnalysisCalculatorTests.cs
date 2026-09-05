using OroBI.Application.Analytics;
using OroBI.Domain.Commercial;
using OroBI.Domain.Imports;

namespace OroBI.Application.Tests.Analytics;

public sealed class TradeAnalysisCalculatorTests
{
    [Fact]
    public void Detail_groups_use_signed_revenue_absolute_trades_and_no_percentage_without_positive_revenue()
    {
        var batch = ImportBatch.Start(ImportFileType.Power, "power.csv", "details").Id;
        CommercialMovement Row(string type, decimal value, decimal qty, string customer = "Cliente A") =>
            Movement(batch, new DateOnly(2026, 8, 1), "ANA", type, value, qty, customer, "P1", "Marca A");
        var result = TradeAnalysisCalculator.Calculate([
            Row("VENDA", 1000m, 10m), Row("DEVOL ENT", -100m, -1m), Row("DEVOLUCAO", -100m, -1m),
            Row("TROCA", 40m, 2m), Row("TROCA DEV", -40m, -2m),
            Row("BONIFICACAO", 500m, 5m), Row("DESC BOLETO", -50m, 0m),
            Row("TROCA", -20m, -1m, "Sem venda"), Row("DEVOLUCAO", -10m, -1m, "Sem venda")]);
        Assert.Equal(6, result.Groups.Count);
        var customer = Assert.Single(result.Groups["customer"], row => row.Label == "Cliente A");
        Assert.Equal(800m, customer.NetRevenue);
        Assert.Equal(80m, customer.TradeValue);
        Assert.Equal(10m, customer.TradePercent);
        Assert.Equal(4m, customer.TradeQuantity);
        Assert.Null(Assert.Single(result.Groups["customer"], row => row.Label == "Sem venda").TradePercent);
        Assert.Equal(100m, Assert.Single(result.Groups["seller"]).TradeValue);
        Assert.All(TradeAnalysisCalculator.Calculate([]).Groups.Values, rows => Assert.Empty(rows));
    }

    [Fact]
    public void Builds_trade_indicators_daily_trend_and_rankings_from_trade_movements()
    {
        var batchId = ImportBatch.Start(ImportFileType.Power, "power.csv", "abc").Id;
        var movements = new[]
        {
            Movement(batchId, new DateOnly(2026, 8, 1), "ANA", "VENDA", 1_000m, 10m, "Cliente A", "P1", "Marca A"),
            Movement(batchId, new DateOnly(2026, 8, 1), "ANA", "DEVOLUCAO", -100m, 1m, "Cliente A", "P1", "Marca A"),
            Movement(batchId, new DateOnly(2026, 8, 2), "ANA", "TROCA DEV", -150m, 3m, "Cliente A", "P1", "Marca A"),
            Movement(batchId, new DateOnly(2026, 8, 2), "BIA", "TROCA", -50m, 2m, "Cliente B", "P2", "Marca B"),
            Movement(batchId, new DateOnly(2026, 8, 3), "BIA", "TROCA DEV", -25m, 1m, "Cliente B", "P2", "Marca B")
        };

        var result = TradeAnalysisCalculator.Calculate(movements);

        Assert.Equal(5, result.FilteredMovementCount);
        Assert.Equal(1_000m, result.GrossSales);
        Assert.Equal(900m, result.NetRevenue);
        Assert.Equal(225m, result.TotalTradeValue);
        Assert.Equal(50m, result.TradeValue);
        Assert.Equal(175m, result.TradeDevValue);
        Assert.Equal(25m, result.TradeToRevenuePercent);
        Assert.Equal(6m, result.TradeQuantity);
        Assert.Equal(3, result.TradeMovementCount);
        Assert.Equal(2, result.CustomerCount);
        Assert.Equal(2, result.ProductCount);
        Assert.Equal(2, result.BrandCount);
        Assert.Collection(result.DailyTrend,
            first => { Assert.Equal(new DateOnly(2026, 8, 2), first.Date); Assert.Equal(200m, first.Value); },
            second => { Assert.Equal(new DateOnly(2026, 8, 3), second.Date); Assert.Equal(25m, second.Value); });
        Assert.Collection(result.SellerRanking,
            first => { Assert.Equal("ANA", first.Name); Assert.Equal(150m, first.Value); },
            second => { Assert.Equal("BIA", second.Name); Assert.Equal(75m, second.Value); });
        Assert.Equal("Cliente A", result.CustomerRanking[0].Name);
        Assert.Equal("P1", result.ProductRanking[0].Name);
        Assert.Equal("Marca A", result.BrandRanking[0].Name);
    }

    private static CommercialMovement Movement(Guid batchId, DateOnly date, string seller, string type, decimal value, decimal quantity, string customer, string product, string brand) =>
        CommercialMovement.CreateFromImport(batchId, date, seller, brand, "Grupo", type, "Cidade", customer, product, value, quantity, 0m, customer, Guid.NewGuid().ToString());
}
