using OroBI.Application.Closings;

namespace OroBI.Application.Tests.Closings;

public sealed class SellerClosingCalculatorTests
{
    [Fact]
    public void Combines_awards_ppp_and_compensation()
    {
        var result = SellerClosingCalculator.Calculate(new SellerClosingInput(
            1000m, 1000m, 10m, 100m, 200m, 100m, 100m, 4m, 5m, 50m, 100m,
            [(10m, 2m, 20m)]));

        Assert.Equal(100m, result.Ppp.Award);
        Assert.Equal(200m, result.RevenueAward);
        Assert.Equal(100m, result.PositivityAward);
        Assert.Equal(50m, result.TradeAward);
        Assert.Equal(100m, result.Compensation.Commission);
        Assert.Equal(450m, result.TotalAwards);
    }
}
