namespace OroBI.Application.Closings;

public static class CompensationCalculator
{
    public static CompensationSummary Calculate(decimal baseSalary, decimal commissionPercent, decimal revenue)
    {
        var commission = revenue * commissionPercent / 100m;
        return new CompensationSummary(commission, baseSalary + commission);
    }
}
