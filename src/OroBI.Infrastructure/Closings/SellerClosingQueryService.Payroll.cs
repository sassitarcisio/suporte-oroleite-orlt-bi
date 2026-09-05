using OroBI.Application.Closings;

namespace OroBI.Infrastructure.Closings;

public sealed partial class SellerClosingQueryService
{
    public async Task<PayrollClosing?> GetPayrollAsync(string coverageSeller, int year, int month, CancellationToken cancellationToken)
    {
        var coverage = PayrollCatalog.CanonicalName(coverageSeller);
        if (!PayrollCatalog.StandardSellers.Contains(coverage))
            throw new ArgumentException("O vendedor de cobertura não faz parte da folha.", nameof(coverageSeller));
        if (await GetImportedDefaultsAsync(cancellationToken) is null) return null;

        var rows = new List<PayrollClosingRow>();
        foreach (var seller in PayrollCatalog.StandardSellers)
        {
            var closing = await GetSellerAsync(seller, year, month, true, cancellationToken);
            if (closing is null) return null;
            rows.Add(new PayrollClosingRow(PayrollCatalog.DisplayName(seller), PayrollCatalog.DisplayName(seller), "",
                closing.Monthly.Revenue, PayrollCatalog.StandardSalary, closing.CommissionPercent,
                closing.Compensation.Commission, closing.Ppp.Award,
                closing.RevenueAward + closing.PositivityAward + closing.TradeAward, 0m));
        }

        var supervisor = await GetAsync("DEIVID MANNES", year, month, cancellationToken);
        var valdir = await GetAsync("VALDIR ZACARIAS", year, month, cancellationToken);
        if (supervisor?.Supervisor is null || valdir is null) return null;
        rows.Add(new PayrollClosingRow("SUPERVISOR: DEIVID MANNES", "DEIVID MANNES", "Fechamento especial Deivid",
            supervisor.Supervisor.Operations.Where(operation => operation.Key is "own" or "team" or "networks").Sum(operation => operation.Revenue),
            supervisor.Compensation.BaseSalary, null, supervisor.Compensation.Commission,
            0m, supervisor.Supervisor.PayrollTeamAverageAward, supervisor.TradeAward));

        var covered = rows.Single(row => row.Seller == PayrollCatalog.DisplayName(coverage));
        rows.Add(covered with { Seller = "TIAGO MARTINS", Reference = $"Cobertura de férias: {covered.Seller}" });

        // Legacy payroll aggregates the full-precision amounts before formatting.
        // The individual Valdir statement still returns its cent-rounded commission.
        rows.Add(new PayrollClosingRow("VALDIR ZACARIAS", "VALDIR ZACARIAS", "Fechamento especial Valdir",
            valdir.Monthly.CommissionableRevenue, valdir.Compensation.BaseSalary, 0.10m,
            valdir.Monthly.CommissionableRevenue * 0.001m, 0m, 0m, valdir.TradeAward));
        return new PayrollClosing(year, month, PayrollCatalog.DisplayName(coverage), PayrollCatalog.CoverageSellers, rows);
    }
}
