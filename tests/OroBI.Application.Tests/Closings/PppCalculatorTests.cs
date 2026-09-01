using OroBI.Application.Closings;

namespace OroBI.Application.Tests.Closings;

public sealed class PppCalculatorTests
{
    [Fact]
    public void Uses_mean_of_active_segment_rates_for_award()
    {
        var result = PppCalculator.Calculate(1200m, [(10m, 2m, 2m), (10m, 0m, 2m)]);

        Assert.Equal(10m, result.MeanPercent);
        Assert.Equal(120m, result.Award);
    }
}
