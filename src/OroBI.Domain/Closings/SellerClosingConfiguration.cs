namespace OroBI.Domain.Closings;

public sealed class SellerClosingConfiguration
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Seller { get; private set; } = string.Empty;
    public int Year { get; private set; }
    public int Month { get; private set; }
    public decimal BaseSalary { get; private set; }
    public decimal CommissionPercent { get; private set; }
    public decimal PppMaximumAward { get; private set; }

    public static SellerClosingConfiguration Create(string seller, int year, int month, decimal baseSalary, decimal commissionPercent, decimal pppMaximumAward) => new()
    {
        Seller = seller.Trim().ToUpperInvariant(),
        Year = year,
        Month = month,
        BaseSalary = baseSalary,
        CommissionPercent = commissionPercent,
        PppMaximumAward = pppMaximumAward
    };
}
