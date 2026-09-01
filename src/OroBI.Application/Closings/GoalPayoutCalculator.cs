namespace OroBI.Application.Closings;

public static class GoalPayoutCalculator
{
    public static decimal Positivity(decimal achievedPercent, decimal prize) =>
        achievedPercent >= 100m ? prize : 0m;

    public static decimal Revenue(decimal achievedPercent, decimal prize)
    {
        if (achievedPercent >= 100m)
        {
            return prize;
        }

        if (achievedPercent >= 90m)
        {
            return prize * 0.75m;
        }

        return achievedPercent >= 80m ? prize * 0.50m : 0m;
    }

    public static decimal Trade(decimal actualPercent, decimal goalPercent, decimal prize) =>
        actualPercent <= goalPercent ? prize : 0m;
}
