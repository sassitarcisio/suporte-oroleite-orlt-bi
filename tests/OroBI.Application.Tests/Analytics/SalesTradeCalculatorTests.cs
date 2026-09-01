using OroBI.Application.Analytics;
using OroBI.Domain.Commercial;
using OroBI.Domain.Imports;

namespace OroBI.Application.Tests.Analytics;

public sealed class SalesTradeCalculatorTests
{
    [Fact]
    public void Calculates_trade_to_revenue_using_legacy_movement_types()
    {
        var batchId = ImportBatch.Start(ImportFileType.Power, "power.csv", "abc").Id;
        var movements = new[]
        {
            CommercialMovement.Create(batchId, new DateOnly(2026, 1, 1), "ANA", "VENDA", 1000m, 1m),
            CommercialMovement.Create(batchId, new DateOnly(2026, 1, 1), "ANA", "DEVOL ENT", -100m, 1m),
            CommercialMovement.Create(batchId, new DateOnly(2026, 1, 1), "ANA", "DEVOLUCAO", -50m, 1m),
            CommercialMovement.Create(batchId, new DateOnly(2026, 1, 1), "ANA", "TROCA", -75m, 1m),
            CommercialMovement.Create(batchId, new DateOnly(2026, 1, 1), "ANA", "TROCA DEV", -25m, 1m)
        };

        var result = SalesTradeCalculator.Calculate(movements);

        Assert.Equal(850m, result.Revenue);
        Assert.Equal(100m, result.Trades);
        Assert.Equal(11.7647m, decimal.Round(result.TradeToRevenuePercent, 4));
    }
}
