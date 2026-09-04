using System.Text.Json;

namespace OroBI.Domain.Closings;

public sealed class ImportedClosingDefaults
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ImportBatchId { get; private set; }
    public decimal? BaseSalary { get; private set; }
    public decimal? CommissionPercent { get; private set; }
    public decimal? PppMaximumAward { get; private set; }
    public string SellerSalariesJson { get; private set; } = "{}";

    public IReadOnlyDictionary<string, decimal> SellerSalaries =>
        JsonSerializer.Deserialize<Dictionary<string, decimal>>(SellerSalariesJson) ?? new Dictionary<string, decimal>();

    public static ImportedClosingDefaults Create(
        Guid importBatchId,
        decimal? baseSalary,
        decimal? commissionPercent,
        decimal? pppMaximumAward,
        IReadOnlyDictionary<string, decimal> sellerSalaries) => new()
        {
            ImportBatchId = importBatchId,
            BaseSalary = baseSalary,
            CommissionPercent = commissionPercent,
            PppMaximumAward = pppMaximumAward,
            SellerSalariesJson = JsonSerializer.Serialize(sellerSalaries.ToDictionary(item => item.Key.Trim().ToUpperInvariant(), item => item.Value))
        };
}
