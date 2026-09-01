using OroBI.Application.Closings;

namespace OroBI.Application.Tests.Closings;

public sealed class CompensationCalculatorTests
{
    [Fact]
    public void Calculates_commission_and_total_salary()
    {
        var result = CompensationCalculator.Calculate(1951m, 1m, 10000m);

        Assert.Equal(100m, result.Commission);
        Assert.Equal(2051m, result.TotalSalary);
    }
}
