using OroBI.Application.Closings;

namespace OroBI.Application.Tests.Closings;

public sealed class StandardClosingCalculatorTests
{
    [Fact]
    public void Pays_each_brand_using_its_own_goals_prizes_and_trade_rate()
    {
        var result = StandardClosingCalculator.Calculate(new StandardClosingInput(
            1000m,
            1951m,
            1m,
            1200m,
            [],
            [
                new ClosingBrandInput("NESTLE", 100m, 100m, 100m, 100m, 1m, 100m, 100m, 25m, 2m),
                new ClosingBrandInput("GALBANI", 100m, 80m, 100m, 90m, 3m, 100m, 100m, 25m, 2m)
            ]));

        Assert.Equal(300m, result.TotalAwards);
        Assert.Equal(225m, result.BrandAwards.Single(item => item.Brand == "NESTLE").TotalAward);
        Assert.Equal(75m, result.BrandAwards.Single(item => item.Brand == "GALBANI").TotalAward);
        Assert.Equal(10m, result.Compensation.Commission);
    }
}
