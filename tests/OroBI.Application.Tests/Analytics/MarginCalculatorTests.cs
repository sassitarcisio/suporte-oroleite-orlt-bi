using OroBI.Application.Analytics;
using OroBI.Domain.Commercial;
using OroBI.Domain.Imports;

namespace OroBI.Application.Tests.Analytics;

public sealed class MarginCalculatorTests
{
    [Fact]
    public void Groups_only_sales_and_counts_customer_codes_with_name_fallback()
    {
        var result = MarginCalculator.Calculate([
            Movement("VENDA", 100m, 2m, 30m, "Cliente", "Leite", "1"),
            Movement("VENDA", 60m, 1m, 20m, "Cliente", "Leite", "2"),
            Movement("VENDA", 40m, 2m, 10m, "Avulso", "Pao", ""),
            Movement("DEVOLUCAO", -900m, -3m, 90m, "Outro", "Outro", "3"),
            Movement("BONIFICACAO", 900m, 3m, 90m, "Outro", "Outro", "3")]);

        Assert.Equal(3, result.CustomerCount);
        Assert.Equal(2, result.ProductCount);
        Assert.Equal(3, result.MovementCount);
        Assert.Equal(200m, result.Revenue);
        Assert.Equal(100m, result.Cost);
        Assert.Equal(50m, result.MarginPercent);
        Assert.Equal(2, result.Groups["customer"].Count);
        var milk = Assert.Single(result.Groups["product"], row => row.Label == "Leite");
        Assert.Equal(160m, milk.Revenue);
        Assert.Equal(80m, milk.Cost);
        Assert.Equal(80m, milk.GrossProfit);
        Assert.Equal(50m, milk.MarginPercent);
        Assert.Equal(3m, milk.Quantity);
        Assert.Equal(3, result.Groups.Count);
        foreach (var rows in result.Groups.Values)
        {
            Assert.Equal(result.Revenue, rows.Sum(row => row.Revenue));
            Assert.Equal(result.Cost, rows.Sum(row => row.Cost));
            Assert.Equal(result.GrossProfit, rows.Sum(row => row.GrossProfit));
            Assert.Equal(5m, rows.Sum(row => row.Quantity));
        }
    }

    [Fact]
    public void Preserves_unknown_groups_and_zero_and_negative_revenue_percentages()
    {
        var zero = MarginCalculator.Calculate([Movement("VENDA", 0m, 1m, 5m, " ", " ", "")]);
        Assert.Equal(0m, zero.MarginPercent);
        Assert.Equal("SEM INFORMAÇÃO", Assert.Single(zero.Groups["product"]).Label);
        Assert.Null(Assert.Single(zero.Groups["product"]).MarginPercent);
        Assert.Equal(-5m, zero.GrossProfit);
        var negative = MarginCalculator.Calculate([Movement("VENDA", -100m, -2m, 30m, "A", "B", "1")]);
        Assert.Equal(-60m, negative.Cost);
        Assert.Equal(40m, negative.MarginPercent);
        Assert.Equal(40m, Assert.Single(negative.Groups["product"]).MarginPercent);
        var empty = MarginCalculator.Calculate([]);
        Assert.Equal(0, empty.CustomerCount);
        Assert.Equal(0, empty.ProductCount);
        Assert.Equal(0, empty.MovementCount);
        Assert.All(empty.Groups.Values, rows => Assert.Empty(rows));
    }

    private static CommercialMovement Movement(string type, decimal value, decimal quantity, decimal cost, string customer, string product, string code) =>
        CommercialMovement.CreateFromImport(Guid.NewGuid(), new DateOnly(2026, 8, 1), "ANA", "MARCA", "GRUPO", type, "CIDADE", customer, product, value, quantity, cost, code, "1");

    [Fact]
    public void Calculates_sale_revenue_cost_profit_and_margin_percent()
    {
        var batchId = ImportBatch.Start(ImportFileType.Power, "power.csv", "abc").Id;
        var sale = CommercialMovement.CreateFromImport(
            batchId,
            new DateOnly(2026, 1, 1),
            "ANA", "NESTLE", "LEITES", "VENDA", "SAO PAULO", "Cliente", "Leite",
            100m, 2m, 30m, "123", "456");
        var returnMovement = CommercialMovement.CreateFromImport(
            batchId,
            new DateOnly(2026, 1, 1),
            "ANA", "NESTLE", "LEITES", "DEVOLUCAO", "SAO PAULO", "Cliente", "Leite",
            -20m, -1m, 30m, "123", "457");

        var result = MarginCalculator.Calculate([sale, returnMovement]);

        Assert.Equal(100m, result.Revenue);
        Assert.Equal(60m, result.Cost);
        Assert.Equal(40m, result.GrossProfit);
        Assert.Equal(40m, result.MarginPercent);
    }
}
