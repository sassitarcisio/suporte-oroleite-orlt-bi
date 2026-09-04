using OroBI.Application.Closings;

namespace OroBI.Application.Tests.Closings;

public sealed class SpecialClosingCalculatorTests
{
    [Theory]
    [InlineData(1.25, 5000)]
    [InlineData(1.75, 3000)]
    [InlineData(2.25, 2000)]
    [InlineData(2.26, 0)]
    public void Deivid_trade_award_uses_approved_bands(decimal tradePercent, decimal expected)
    {
        Assert.Equal(expected, SpecialClosingCalculator.DeividTradeAward(tradePercent));
    }

    [Theory]
    [InlineData(2, 5000)]
    [InlineData(3, 3000)]
    [InlineData(4, 2000)]
    [InlineData(4.01, 0)]
    public void Valdir_trade_award_uses_approved_bands(decimal tradePercent, decimal expected)
    {
        Assert.Equal(expected, SpecialClosingCalculator.ValdirTradeAward(tradePercent));
    }

    [Fact]
    public void Calculates_Deivid_salary_commissions_and_separate_awards()
    {
        var result = SpecialClosingCalculator.CalculateDeivid(new DeividClosingInput(
            3000m,
            10000m,
            200000m,
            50000m,
            400m,
            1.25m));

        Assert.Equal(475m, result.Commission);
        Assert.Equal(3475m, result.SalaryAndCommission);
        Assert.Equal(400m, result.TeamAward);
        Assert.Equal(5000m, result.TradeAward);
        Assert.Equal(5400m, result.TotalAwards);
        Assert.Equal(8875m, result.Total);
    }

    [Fact]
    public void Calculates_Valdir_salary_commission_and_trade_award()
    {
        var result = SpecialClosingCalculator.CalculateValdir(new ValdirClosingInput(4000m, 1000000m, 3m));

        Assert.Equal(1000m, result.Commission);
        Assert.Equal(5000m, result.SalaryAndCommission);
        Assert.Equal(3000m, result.TradeAward);
        Assert.Equal(8000m, result.Total);
    }
}
