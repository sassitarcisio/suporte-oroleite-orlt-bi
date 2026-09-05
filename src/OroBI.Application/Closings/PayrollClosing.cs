namespace OroBI.Application.Closings;

public interface IPayrollClosingQueryService
{
    Task<PayrollClosing?> GetPayrollAsync(string coverageSeller, int year, int month, CancellationToken cancellationToken);
}

public sealed record PayrollClosingRow(string Seller, string SourceSeller, string Reference,
    decimal Revenue, decimal BaseSalary, decimal? CommissionPercent, decimal Commission,
    decimal PppAward, decimal GoalAward, decimal TradeAward)
{
    public decimal Incentives => PppAward + GoalAward + TradeAward;
    public decimal Total => BaseSalary + Commission + Incentives;
}

public sealed record PayrollClosing(int Year, int Month, string CoverageSeller,
    IReadOnlyList<string> CoverageSellers, IReadOnlyList<PayrollClosingRow> Rows)
{
    public int SellerCount => Rows.Count;
    public decimal TotalBaseSalary => Rows.Sum(row => row.BaseSalary);
    public decimal TotalCommission => Rows.Sum(row => row.Commission);
    public decimal TotalPppAward => Rows.Sum(row => row.PppAward);
    public decimal TotalGoalAward => Rows.Sum(row => row.GoalAward);
    public decimal TotalIncentives => Rows.Sum(row => row.Incentives);
    public decimal Total => Rows.Sum(row => row.Total);
}

public static class PayrollCatalog
{
    public const decimal StandardSalary = 1951m;
    public const string DefaultCoverage = "MARCIO LUIZ DA ROSA";
    public static IReadOnlyList<string> StandardSellers { get; } = Array.AsReadOnly(new[]
    {
        "ANDERSON GONCALVES SOUZA", "MARCELO IVONEI DA ROSA", "MARCIO FERNANDES",
        "MARCIO LUIZ DA ROSA", "RAMON DO NASCIMENTO", "RODRIGO KEHL"
    });
    public static IReadOnlyList<string> CoverageSellers { get; } = Array.AsReadOnly(StandardSellers.Select(DisplayName).ToArray());
    public static IReadOnlySet<string> Brands { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "NESTLE", "GALBANI", "ZINHO", "LIFE", "PECCIN", "NOTCO", "VISCONTI", "BAUDUCCO"
    };
    public static string DisplayName(string seller) => seller == "RODRIGO KEHL" ? "RODRIGO" : seller;
    public static string CanonicalName(string seller) => seller.Trim().ToUpperInvariant() == "RODRIGO" ? "RODRIGO KEHL" : seller.Trim().ToUpperInvariant();
}
