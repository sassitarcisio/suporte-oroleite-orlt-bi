using OroBI.Application.Analytics;
using OroBI.Domain.Commercial;
using OroBI.Domain.Imports;

namespace OroBI.Application.Tests.Analytics;

public sealed class MarginCalculatorTests
{
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
