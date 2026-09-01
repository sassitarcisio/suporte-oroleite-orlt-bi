using OroBI.Application.Analytics;
using OroBI.Domain.Commercial;
using OroBI.Domain.Imports;

namespace OroBI.Application.Tests.Analytics;

public sealed class TradeCalculatorTests
{
    [Fact]
    public void Calculates_physical_trades_and_trade_to_sales_percent()
    {
        var batchId = ImportBatch.Start(ImportFileType.Power, "power.csv", "abc").Id;
        var movements = new[]
        {
            CommercialMovement.Create(batchId, new DateOnly(2026, 1, 1), "ANA", "VENDA", 1000m, 10m),
            CommercialMovement.Create(batchId, new DateOnly(2026, 1, 1), "ANA", "TROCA", -50m, -1m),
            CommercialMovement.Create(batchId, new DateOnly(2026, 1, 1), "ANA", "TROCA DEV", -25m, -1m),
            CommercialMovement.Create(batchId, new DateOnly(2026, 1, 1), "ANA", "DEVOLUCAO", -10m, -1m)
        };

        var result = TradeCalculator.Calculate(movements);

        Assert.Equal(75m, result.PhysicalTrades);
        Assert.Equal(7.5m, result.TradeToSalesPercent);
        Assert.Equal(2, result.TradeMovementCount);
    }
}
