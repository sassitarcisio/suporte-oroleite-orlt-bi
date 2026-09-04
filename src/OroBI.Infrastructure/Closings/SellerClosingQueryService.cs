using Microsoft.EntityFrameworkCore;
using OroBI.Application.Closings;
using OroBI.Domain.Imports;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Closings;

public sealed class SellerClosingQueryService(OroBiDbContext dbContext) : ISellerClosingQueryService
{
    private static readonly string[] DeividTeam =
    [
        "ANDERSON GONCALVES SOUZA", "MARCELO IVONEI DA ROSA", "MARCIO FERNANDES",
        "MARCIO LUIZ DA ROSA", "PAULO RICARDO LOPES", "RAMON DO NASCIMENTO", "RODRIGO KEHL"
    ];

    public async Task<SellerClosingSummary?> GetAsync(string seller, int year, int month, CancellationToken cancellationToken)
    {
        var normalizedSeller = seller.Trim().ToUpperInvariant();
        var configuration = await dbContext.SellerClosingConfigurations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Seller == normalizedSeller && item.Year == year && item.Month == month, cancellationToken);
        var importedDefaults = await dbContext.ImportedClosingDefaults.AsNoTracking()
            .Join(dbContext.ImportBatches.AsNoTracking(), defaults => defaults.ImportBatchId, batch => batch.Id, (defaults, batch) => new { defaults, batch })
            .Where(item => item.batch.FileType == ImportFileType.GoalValues &&
                (item.batch.Status == ImportBatchStatus.Completed || item.batch.Status == ImportBatchStatus.CompletedWithErrors))
            .OrderByDescending(item => item.batch.StartedAtUtc)
            .Select(item => item.defaults)
            .FirstOrDefaultAsync(cancellationToken);
        if (normalizedSeller == "VALDIR ZACARIAS")
        {
            return await GetValdirClosingAsync(year, month, configuration?.BaseSalary ?? ImportedSellerSalary(importedDefaults, normalizedSeller), cancellationToken);
        }
        if (normalizedSeller == "DEIVID MANNES")
        {
            return await GetDeividClosingAsync(year, month, configuration?.BaseSalary ?? ImportedSellerSalary(importedDefaults, normalizedSeller), cancellationToken);
        }

        var baseSalary = configuration?.BaseSalary ?? ImportedSellerSalary(importedDefaults, normalizedSeller) ?? importedDefaults?.BaseSalary;
        var commissionPercent = configuration?.CommissionPercent ?? importedDefaults?.CommissionPercent;
        var pppMaximumAward = configuration?.PppMaximumAward ?? importedDefaults?.PppMaximumAward;
        if (baseSalary is null || commissionPercent is null || pppMaximumAward is null) return null;
        var movements = await dbContext.CommercialMovements.AsNoTracking()
            .Where(item => item.Seller == normalizedSeller && item.MovementDate.Year == year && item.MovementDate.Month == month).ToListAsync(cancellationToken);
        var goals = await dbContext.GoalRecords.AsNoTracking()
            .Where(item => item.Seller == normalizedSeller && item.Year == year && item.Month == month).ToListAsync(cancellationToken);
        var ppp = await dbContext.PppRecords.AsNoTracking()
            .Where(item => item.Seller == normalizedSeller && item.Year == year && item.Month == month).ToListAsync(cancellationToken);
        var values = importedDefaults is null
            ? new List<OroBI.Domain.Goals.GoalValueRecord>()
            : await dbContext.GoalValueRecords.AsNoTracking()
                .Where(item => item.ImportBatchId == importedDefaults.ImportBatchId)
                .ToListAsync(cancellationToken);
        var brands = values.Select(value =>
        {
            var brandGoals = goals.Where(goal => goal.Description.Contains(value.Brand, StringComparison.OrdinalIgnoreCase)).ToArray();
            var revenueGoal = brandGoals.FirstOrDefault(goal => goal.GoalType == "FATURAMENTO");
            var positivityGoal = brandGoals.FirstOrDefault(goal => goal.GoalType == "POSITIVACAO");
            var brandMovements = movements.Where(movement => movement.Brand == value.Brand).ToArray();
            var revenue = brandMovements.Where(movement => movement.MovementType is "VENDA" or "DEVOLUCAO").Sum(movement => movement.TotalValue);
            var total = brandMovements.Sum(movement => movement.TotalValue);
            var trade = brandMovements.Where(movement => movement.MovementType is "TROCA" or "TROCA DEV").Sum(movement => decimal.Abs(movement.TotalValue));
            return new ClosingBrandInput(value.Brand, positivityGoal?.Target ?? 0m, positivityGoal?.Achieved ?? 0m, revenueGoal?.Target ?? 0m, revenueGoal?.Achieved ?? 0m, decimal.Abs(total) == 0m ? 0m : trade / decimal.Abs(total) * 100m, value.PositivityPrize, value.RevenuePrize, value.TradePrize, value.TradePercentageGoal);
        }).ToArray();
        var commissionableRevenue = movements.Where(item => item.MovementType != "BONIFICACAO").Sum(item => item.TotalValue);
        var standard = StandardClosingCalculator.Calculate(new StandardClosingInput(commissionableRevenue, baseSalary.Value, commissionPercent.Value, pppMaximumAward.Value, ppp.Select(item => ((decimal)item.CustomerCount, (decimal)item.ItemsPerSegment, (decimal)item.GroupsPlaced)).ToArray(), brands));
        return new SellerClosingSummary(standard.Ppp, standard.BrandAwards.Sum(item => item.RevenueAward), standard.BrandAwards.Sum(item => item.PositivityAward), standard.BrandAwards.Sum(item => item.TradeAward), standard.Compensation, standard.TotalAwards);
    }

    private async Task<SellerClosingSummary?> GetValdirClosingAsync(int year, int month, decimal? baseSalary, CancellationToken cancellationToken)
    {
        if (baseSalary is null) return null;
        var movements = await dbContext.CommercialMovements.AsNoTracking()
            .Where(item => item.MovementDate.Year == year && item.MovementDate.Month == month &&
                item.Seller != "OPERACAO BAUDUCCO" && item.MovementType != "BONIFICACAO")
            .ToListAsync(cancellationToken);
        var commissionableRevenue = movements.Sum(item => item.TotalValue);
        var trade = movements.Where(item => item.MovementType is "TROCA" or "TROCA DEV").Sum(item => decimal.Abs(item.TotalValue));
        var tradePercent = decimal.Abs(commissionableRevenue) == 0m ? 0m : trade / decimal.Abs(commissionableRevenue) * 100m;
        var special = SpecialClosingCalculator.CalculateValdir(new ValdirClosingInput(baseSalary.Value, commissionableRevenue, tradePercent));
        return new SellerClosingSummary(new PppSummary(0m, 0m), 0m, 0m, special.TradeAward, new CompensationSummary(special.Commission, special.SalaryAndCommission), special.TotalAwards);
    }

    private async Task<SellerClosingSummary?> GetDeividClosingAsync(int year, int month, decimal? baseSalary, CancellationToken cancellationToken)
    {
        if (baseSalary is null) return null;
        var movements = await dbContext.CommercialMovements.AsNoTracking()
            .Where(item => item.MovementDate.Year == year && item.MovementDate.Month == month && item.MovementType != "BONIFICACAO")
            .ToListAsync(cancellationToken);
        var ownRevenue = movements.Where(item => item.Seller == "DEIVID MANNES").Sum(item => item.TotalValue);
        var teamRevenue = movements.Where(item => DeividTeam.Contains(item.Seller)).Sum(item => item.TotalValue);
        var networkRevenue = movements.Where(item => item.Seller != "OPERACAO BAUDUCCO" && (item.Group == "BISTEK" || item.Group == "GIASSI")).Sum(item => item.TotalValue);
        var totalRevenue = movements.Sum(item => item.TotalValue);
        var trade = movements.Where(item => item.MovementType is "TROCA" or "TROCA DEV").Sum(item => decimal.Abs(item.TotalValue));
        var tradePercent = decimal.Abs(totalRevenue) == 0m ? 0m : trade / decimal.Abs(totalRevenue) * 100m;
        var teamAwards = new List<decimal>();
        foreach (var teamSeller in DeividTeam)
        {
            var summary = await GetAsync(teamSeller, year, month, cancellationToken);
            teamAwards.Add(summary?.TotalAwards ?? 0m);
        }

        var special = SpecialClosingCalculator.CalculateDeivid(new DeividClosingInput(baseSalary.Value, ownRevenue, teamRevenue, networkRevenue, teamAwards.Average(), tradePercent));
        return new SellerClosingSummary(new PppSummary(0m, 0m), special.TeamAward, 0m, special.TradeAward, new CompensationSummary(special.Commission, special.SalaryAndCommission), special.TotalAwards);
    }

    private static decimal? ImportedSellerSalary(OroBI.Domain.Closings.ImportedClosingDefaults? importedDefaults, string seller)
    {
        if (importedDefaults is null) return null;
        foreach (var entry in importedDefaults.SellerSalaries)
        {
            var normalizedKey = entry.Key.Trim().ToUpperInvariant();
            var separatorIndex = normalizedKey.IndexOf(':');
            var candidate = separatorIndex >= 0 ? normalizedKey[(separatorIndex + 1)..].Trim() : normalizedKey;
            if (candidate == seller) return entry.Value;
        }

        return null;
    }
}
