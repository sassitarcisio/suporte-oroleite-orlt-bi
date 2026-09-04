using OroBI.Application.Analytics;

namespace OroBI.Application.Tests.Analytics;

public sealed class NetMarginReportTests
{
    [Fact]
    public void Calculates_liquid_profit_after_returns_and_losses()
    {
        var report = NetMarginReport.Create(
            grossSales: 100m,
            returns: 10m,
            netCost: 40m,
            tradeLosses: 20m,
            boletoDiscounts: 5m,
            productCount: 3);

        Assert.Equal(90m, report.NetSales);
        Assert.Equal(25m, report.LiquidProfit);
        Assert.Equal(27.778m, decimal.Round(report.LiquidMarginPercent, 3));
    }
}
