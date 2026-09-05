namespace OroBI.Application.Closings;

public sealed record CompensationSummary(decimal Commission, decimal TotalSalary)
{
    public decimal BaseSalary => TotalSalary - Commission;
}
