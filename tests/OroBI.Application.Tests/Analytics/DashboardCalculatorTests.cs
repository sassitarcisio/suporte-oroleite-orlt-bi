using OroBI.Application.Analytics;
using OroBI.Domain.Commercial;
using OroBI.Domain.Imports;

namespace OroBI.Application.Tests.Analytics;

public sealed class DashboardCalculatorTests
{
    [Fact]
    public void Preserves_legacy_sales_and_negative_logic()
    {
        var batchId = ImportBatch.Start(ImportFileType.Power, "power.csv", "abc").Id;
        var movements = new[]
        {
            CommercialMovement.Create(batchId, new DateOnly(2026, 1, 1), "ANA", "VENDA", 100m, 2m),
            CommercialMovement.Create(batchId, new DateOnly(2026, 1, 1), "ANA", "DEVOLUCAO", -20m, 1m),
            CommercialMovement.Create(batchId, new DateOnly(2026, 1, 1), "ANA", "TROCA", -10m, 1m)
        };

        var result = DashboardCalculator.Calculate(movements);

        Assert.Equal(100m, result.GrossSales);
        Assert.Equal(70m, result.NetResult);
        Assert.Equal(30m, result.NegativeMovements);
        Assert.Equal(30m, result.NegativePercent);
        Assert.Equal(2m, result.SaleQuantity);
        Assert.Equal(3, result.MovementCount);
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
