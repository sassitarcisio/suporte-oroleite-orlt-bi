using OroBI.Application.Analytics;
using OroBI.Domain.Commercial;
using OroBI.Domain.Imports;

namespace OroBI.Application.Tests.Analytics;

public sealed class DashboardCalculatorTests
{
    [Fact]
    public void Groups_signed_values_all_metrics_and_distinct_documents_without_truncating_dynamic_data()
    {
        var id = Guid.NewGuid();
        CommercialMovement Row(string type, decimal value, decimal quantity, string document, string brand = "MARCA") =>
            CommercialMovement.CreateFromImport(id, new DateOnly(2026, 8, 1), "ANA", brand, "REDE", type,
                "CIDADE", "Cliente A", "Produto A", value, quantity, 1m, "C1", document);
        var rows = new[] { Row("VENDA", 100m, 10m, "D1"), Row("VENDA", 50m, 5m, "D1"),
            Row("DEVOLUCAO", -20m, -2m, "D2"), Row("TROCA", 3m, 1m, ""), Row("DESC BOLETO", -7m, 0m, "", "") };
        var result = DashboardCalculator.BuildDetails(rows);
        foreach (var key in new[] { "seller", "customer", "group", "product", "city", "family", "date" })
        {
            var aggregate = Assert.Single(result.Groups[key]);
            Assert.Equal(126m, aggregate.NetResult);
            Assert.Equal(150m, aggregate.GrossSales);
            Assert.Equal(27m, aggregate.NegativeMovements);
            Assert.Equal(14m, aggregate.Quantity);
            Assert.Equal(5, aggregate.MovementCount);
            Assert.Equal(2, aggregate.DocumentCount);
        }
        Assert.Equal(-7m, result.Groups["brand"].Single(row => row.Label == "SEM INFORMAÇÃO").NetResult);
        Assert.Equal(3m, result.Groups["movementType"].Single(row => row.Label == "TROCA").NetResult);
        Assert.Equal(-20m, result.Groups["movementType"].Single(row => row.Label == "DEVOLUCAO").NetResult);
        var many = Enumerable.Range(0, 15).Select(index => Row("VENDA", index, 1m, "D1", $"Marca {index}"));
        Assert.Equal(15, DashboardCalculator.BuildDetails(many).Groups["brand"].Length);
        Assert.All(DashboardCalculator.BuildDetails([]).Groups.Values, values => Assert.Empty(values));
    }

    [Fact]
    public void Preserves_legacy_sales_and_negative_logic()
    {
        var batchId = ImportBatch.Start(ImportFileType.Power, "power.csv", "abc").Id;
        var movements = new[]
        {
            CommercialMovement.CreateFromImport(batchId, new DateOnly(2026, 1, 1), "ANA", "OROLEITE", "LEITES", "VENDA", "GOIANIA", "Mercado A", "Leite", 100m, 2m, 1m, "C1", "D1"),
            CommercialMovement.CreateFromImport(batchId, new DateOnly(2026, 1, 1), "ANA", "OROLEITE", "LEITES", "DEVOLUCAO", "GOIANIA", "Mercado A", "Leite", -20m, 1m, 1m, "C1", "D2"),
            CommercialMovement.CreateFromImport(batchId, new DateOnly(2026, 1, 1), "ANA", "OROLEITE", "LEITES", "TROCA", "GOIANIA", "Mercado B", "Leite", -10m, 1m, 1m, "C2", "D2")
        };

        var result = DashboardCalculator.Calculate(movements);

        Assert.Equal(100m, result.GrossSales);
        Assert.Equal(70m, result.NetResult);
        Assert.Equal(30m, result.NegativeMovements);
        Assert.Equal(30m, result.NegativePercent);
        Assert.Equal(2m, result.SaleQuantity);
        Assert.Equal(3, result.MovementCount);
        Assert.Equal(2, result.CustomerCount);
        Assert.Equal(2, result.DocumentCount);
    }

    [Fact]
    public void Builds_daily_trend_and_seller_ranking_from_filtered_movements()
    {
        var batchId = ImportBatch.Start(ImportFileType.Power, "power.csv", "abc").Id;
        var movements = new[]
        {
            CommercialMovement.Create(batchId, new DateOnly(2026, 8, 1), "ANA", "VENDA", 100m, 1m),
            CommercialMovement.Create(batchId, new DateOnly(2026, 8, 1), "ANA", "DEVOLUCAO", -20m, 1m),
            CommercialMovement.Create(batchId, new DateOnly(2026, 8, 2), "BRUNO", "VENDA", 180m, 1m)
        };

        var result = DashboardCalculator.BuildDetails(movements);

        Assert.Collection(result.DailyTrend,
            point => { Assert.Equal(new DateOnly(2026, 8, 1), point.Date); Assert.Equal(100m, point.GrossSales); Assert.Equal(80m, point.NetResult); },
            point => { Assert.Equal(new DateOnly(2026, 8, 2), point.Date); Assert.Equal(180m, point.GrossSales); Assert.Equal(180m, point.NetResult); });
        Assert.Collection(result.SellerResults,
            seller => { Assert.Equal("BRUNO", seller.Seller); Assert.Equal(180m, seller.NetResult); },
            seller => { Assert.Equal("ANA", seller.Seller); Assert.Equal(80m, seller.NetResult); });
    }
}
