using OroBI.Application.Closings;

namespace OroBI.Application.Tests.Closings;

public sealed class GoalPayoutCalculatorTests
{
    [Theory]
    [InlineData(100, 1000, 1000)]
    [InlineData(90, 1000, 750)]
    [InlineData(80, 1000, 500)]
    [InlineData(79.99, 1000, 0)]
    public void Revenue_prize_uses_legacy_tiers(decimal achievedPercent, decimal prize, decimal expected)
    {
        var result = GoalPayoutCalculator.Revenue(achievedPercent, prize);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(100, 500, 500)]
    [InlineData(99.99, 500, 0)]
    public void Positivity_prize_requires_full_goal(decimal achievedPercent, decimal prize, decimal expected)
    {
        Assert.Equal(expected, GoalPayoutCalculator.Positivity(achievedPercent, prize));
    }

    [Theory]
    [InlineData(2, 2, 300)]
    [InlineData(1.5, 2, 300)]
    [InlineData(2.01, 2, 0)]
    public void Trade_prize_requires_actual_percent_at_or_below_goal(decimal actualPercent, decimal goalPercent, decimal expected)
    {
        Assert.Equal(expected, GoalPayoutCalculator.Trade(actualPercent, goalPercent, 300m));
    }
}
