using Microsoft.EntityFrameworkCore;
using OroBI.Application.Analytics;
using OroBI.Application.Closings;
using OroBI.Domain.Commercial;
using OroBI.Domain.Goals;
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
        var requestedSeller = seller.Trim().ToUpperInvariant();
        var importedSeller = SellerAliasCatalog.ResolveImportedName(requestedSeller);
        var configuration = await dbContext.SellerClosingConfigurations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Seller == requestedSeller && item.Year == year && item.Month == month, cancellationToken);
        var importedDefaults = await dbContext.ImportedClosingDefaults.AsNoTracking()
            .Join(dbContext.ImportBatches.AsNoTracking(), defaults => defaults.ImportBatchId, batch => batch.Id, (defaults, batch) => new { defaults, batch })
            .Where(item => item.batch.FileType == ImportFileType.GoalValues &&
                (item.batch.Status == ImportBatchStatus.Completed || item.batch.Status == ImportBatchStatus.CompletedWithErrors))
            .OrderByDescending(item => item.batch.StartedAtUtc)
            .Select(item => item.defaults)
            .FirstOrDefaultAsync(cancellationToken);
        if (requestedSeller == "VALDIR ZACARIAS")
        {
            return await GetValdirClosingAsync(year, month, configuration?.BaseSalary ?? ImportedSellerSalary(importedDefaults, requestedSeller), cancellationToken);
        }
        if (requestedSeller == "DEIVID MANNES")
        {
            return await GetDeividClosingAsync(year, month, configuration?.BaseSalary ?? ImportedSellerSalary(importedDefaults, requestedSeller), importedDefaults, cancellationToken);
        }

        var baseSalary = configuration?.BaseSalary ?? ImportedSellerSalary(importedDefaults, requestedSeller) ?? ImportedSellerSalary(importedDefaults, importedSeller) ?? importedDefaults?.BaseSalary;
        var commissionPercent = configuration?.CommissionPercent ?? importedDefaults?.CommissionPercent;
        var pppMaximumAward = configuration?.PppMaximumAward ?? importedDefaults?.PppMaximumAward;
        if (baseSalary is null || commissionPercent is null || pppMaximumAward is null) return null;
        var movements = await dbContext.CommercialMovements.AsNoTracking()
            .Where(item => item.Seller == importedSeller && item.MovementDate.Year == year && item.MovementDate.Month == month).ToListAsync(cancellationToken);
        var goals = await dbContext.GoalRecords.AsNoTracking()
            .Where(item => item.Seller == importedSeller && item.Year == year && item.Month == month).ToListAsync(cancellationToken);
        var ppp = await dbContext.PppRecords.AsNoTracking()
            .Where(item => item.Seller == importedSeller && item.Year == year && item.Month == month).ToListAsync(cancellationToken);
        var values = importedDefaults is null
            ? new List<OroBI.Domain.Goals.GoalValueRecord>()
            : await dbContext.GoalValueRecords.AsNoTracking()
                .Where(item => item.ImportBatchId == importedDefaults.ImportBatchId)
                .ToListAsync(cancellationToken);
        var brands = BuildBrandInputs(values, goals, movements);
        var commissionableRevenue = movements.Where(item => item.MovementType != "BONIFICACAO").Sum(item => item.TotalValue);
        var standard = StandardClosingCalculator.Calculate(new StandardClosingInput(commissionableRevenue, baseSalary.Value, commissionPercent.Value, pppMaximumAward.Value, ppp.Select(item => ((decimal)item.CustomerCount, (decimal)item.ItemsPerSegment, (decimal)item.GroupsPlaced)).ToArray(), brands));
        return new SellerClosingSummary(standard.Ppp, standard.BrandAwards.Sum(item => item.RevenueAward), standard.BrandAwards.Sum(item => item.PositivityAward), standard.BrandAwards.Sum(item => item.TradeAward), standard.Compensation, standard.TotalAwards)
        {
            BrandAwards = standard.BrandAwards
        };
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

    private async Task<SellerClosingSummary?> GetDeividClosingAsync(int year, int month, decimal? baseSalary, OroBI.Domain.Closings.ImportedClosingDefaults? importedDefaults, CancellationToken cancellationToken)
    {
        if (baseSalary is null) return null;
        var importedTeam = DeividTeam.Select(SellerAliasCatalog.ResolveImportedName).ToHashSet(StringComparer.Ordinal);
        var movements = await dbContext.CommercialMovements.AsNoTracking()
            .Where(item => item.MovementDate.Year == year && item.MovementDate.Month == month && item.MovementType != "BONIFICACAO")
            .ToListAsync(cancellationToken);
        var ownRevenue = movements.Where(item => item.Seller == SellerAliasCatalog.ResolveImportedName("DEIVID MANNES")).Sum(item => item.TotalValue);
        var teamRevenue = movements.Where(item => importedTeam.Contains(item.Seller)).Sum(item => item.TotalValue);
        var networkRevenue = movements.Where(item => item.Seller != "OPERACAO BAUDUCCO" && (item.Group == "BISTEK" || item.Group == "GIASSI")).Sum(item => item.TotalValue);
        var totalRevenue = movements.Sum(item => item.TotalValue);
        var trade = movements.Where(item => item.MovementType is "TROCA" or "TROCA DEV").Sum(item => decimal.Abs(item.TotalValue));
        var tradePercent = decimal.Abs(totalRevenue) == 0m ? 0m : trade / decimal.Abs(totalRevenue) * 100m;
        var teamGoals = await dbContext.GoalRecords.AsNoTracking()
            .Where(item => DeividTeam.Contains(item.Seller) && item.Year == year && item.Month == month)
            .ToListAsync(cancellationToken);
        var teamPpp = await dbContext.PppRecords.AsNoTracking()
            .Where(item => DeividTeam.Contains(item.Seller) && item.Year == year && item.Month == month)
            .ToListAsync(cancellationToken);
        var teamConfigurations = await dbContext.SellerClosingConfigurations.AsNoTracking()
            .Where(item => DeividTeam.Contains(item.Seller) && item.Year == year && item.Month == month)
            .ToListAsync(cancellationToken);
        var values = importedDefaults is null
            ? new List<GoalValueRecord>()
            : await dbContext.GoalValueRecords.AsNoTracking()
                .Where(item => item.ImportBatchId == importedDefaults.ImportBatchId)
                .ToListAsync(cancellationToken);
        var teamAwards = DeividTeam.Select(teamSeller =>
        {
            var pppMaximumAward = teamConfigurations.FirstOrDefault(item => item.Seller == teamSeller)?.PppMaximumAward ?? importedDefaults?.PppMaximumAward;
            if (pppMaximumAward is null) return 0m;
            var standard = StandardClosingCalculator.Calculate(new StandardClosingInput(
                0m,
                0m,
                0m,
                pppMaximumAward.Value,
                teamPpp.Where(item => item.Seller == teamSeller).Select(item => ((decimal)item.CustomerCount, (decimal)item.ItemsPerSegment, (decimal)item.GroupsPlaced)).ToArray(),
                BuildBrandInputs(values, teamGoals.Where(item => item.Seller == teamSeller), movements.Where(item => item.Seller == SellerAliasCatalog.ResolveImportedName(teamSeller)))));
            return standard.TotalAwards;
        }).ToArray();

        var special = SpecialClosingCalculator.CalculateDeivid(new DeividClosingInput(baseSalary.Value, ownRevenue, teamRevenue, networkRevenue, teamAwards.Average(), tradePercent));
        return new SellerClosingSummary(new PppSummary(0m, 0m), special.TeamAward, 0m, special.TradeAward, new CompensationSummary(special.Commission, special.SalaryAndCommission), special.TotalAwards);
    }

    private static ClosingBrandInput[] BuildBrandInputs(IEnumerable<GoalValueRecord> values, IEnumerable<GoalRecord> goals, IEnumerable<CommercialMovement> movements) =>
        values.Select(value =>
        {
            var brandGoals = goals.Where(goal => goal.Description.Contains(value.Brand, StringComparison.OrdinalIgnoreCase)).ToArray();
            var revenueGoal = brandGoals.FirstOrDefault(goal => goal.GoalType == "FATURAMENTO");
            var positivityGoal = brandGoals.FirstOrDefault(goal => goal.GoalType == "POSITIVACAO");
            var brandMovements = movements.Where(movement => movement.Brand == value.Brand).ToArray();
            var total = brandMovements.Sum(movement => movement.TotalValue);
            var trade = brandMovements.Where(movement => movement.MovementType is "TROCA" or "TROCA DEV").Sum(movement => decimal.Abs(movement.TotalValue));
            return new ClosingBrandInput(value.Brand, positivityGoal?.Target ?? 0m, positivityGoal?.Achieved ?? 0m, revenueGoal?.Target ?? 0m, revenueGoal?.Achieved ?? 0m, decimal.Abs(total) == 0m ? 0m : trade / decimal.Abs(total) * 100m, value.PositivityPrize, value.RevenuePrize, value.TradePrize, value.TradePercentageGoal);
        }).ToArray();

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
