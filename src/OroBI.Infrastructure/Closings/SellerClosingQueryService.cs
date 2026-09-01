using Microsoft.EntityFrameworkCore;
using OroBI.Application.Closings;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Closings;

public sealed class SellerClosingQueryService(OroBiDbContext dbContext) : ISellerClosingQueryService
{
    public async Task<SellerClosingSummary?> GetAsync(string seller, int year, int month, CancellationToken cancellationToken)
    {
        var normalizedSeller = seller.Trim().ToUpperInvariant();
        var configuration = await dbContext.SellerClosingConfigurations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Seller == normalizedSeller && item.Year == year && item.Month == month, cancellationToken);
        if (configuration is null) return null;
        var movements = await dbContext.CommercialMovements.AsNoTracking()
            .Where(item => item.Seller == normalizedSeller && item.MovementDate.Year == year && item.MovementDate.Month == month).ToListAsync(cancellationToken);
        var goals = await dbContext.GoalRecords.AsNoTracking()
            .Where(item => item.Seller == normalizedSeller && item.Year == year && item.Month == month).ToListAsync(cancellationToken);
        var ppp = await dbContext.PppRecords.AsNoTracking()
            .Where(item => item.Seller == normalizedSeller && item.Year == year && item.Month == month).ToListAsync(cancellationToken);
        var values = await dbContext.GoalValueRecords.AsNoTracking().ToListAsync(cancellationToken);
        decimal Percent(string type) { var goal = goals.FirstOrDefault(item => item.GoalType == type); return goal is null || goal.Target == 0m ? 0m : goal.Achieved / goal.Target * 100m; }
        var tradeGoal = goals.FirstOrDefault(item => item.GoalType == "TROCA");
        var revenue = movements.Where(item => item.MovementType is "VENDA" or "DEVOLUCAO").Sum(item => item.TotalValue);
        var trade = movements.Where(item => item.MovementType is "TROCA" or "TROCA DEV").Sum(item => decimal.Abs(item.TotalValue));
        var tradeActualPercent = decimal.Abs(revenue) == 0m ? 0m : trade / decimal.Abs(revenue) * 100m;
        return SellerClosingCalculator.Calculate(new SellerClosingInput(revenue, configuration.BaseSalary, configuration.CommissionPercent, Percent("FATURAMENTO"), values.Sum(item => item.RevenuePrize), Percent("POSITIVACAO"), values.Sum(item => item.PositivityPrize), tradeActualPercent, tradeGoal?.Target ?? 0m, values.Sum(item => item.TradePrize), configuration.PppMaximumAward, ppp.Select(item => ((decimal)item.CustomerCount, (decimal)item.ItemsPerSegment, (decimal)item.GroupsPlaced)).ToArray()));
    }
}
