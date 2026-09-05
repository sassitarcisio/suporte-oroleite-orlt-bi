using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OroBI.Application.Analytics;
using OroBI.Application.Closings;
using OroBI.Application.Portal;
using OroBI.Domain.Commercial;
using OroBI.Domain.Goals;
using OroBI.Domain.Imports;
using OroBI.Domain.Synchronization;
using OroBI.Infrastructure.Analytics;
using OroBI.Infrastructure.Imports;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Portal;

public sealed class PortalQueryService(OroBiDbContext dbContext, ISellerClosingQueryService closingService,
    TimeProvider? timeProvider = null) : IPortalQueryService
{
    private const int ListLimit = 200;
    private const string Unavailable = "Valores de premio indisponiveis para este vendedor e mes; confira a configuracao do fechamento.";
    private static readonly TimeZoneInfo BrazilTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public async Task<PortalDashboard> GetDashboardAsync(string seller, CommercialFilter filter, CancellationToken cancellationToken)
    {
        var today = Today();
        filter = NormalizeFilter(seller, filter);
        var rows = await (await QueryAsync(seller, filter, cancellationToken)).ToListAsync(cancellationToken);
        var monthFilter = filter with { StartDate = new(today.Year, today.Month, 1), EndDate = new(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)) };
        var monthRows = filter.StartDate == monthFilter.StartDate && filter.EndDate == monthFilter.EndDate
            ? rows : await (await QueryAsync(seller, monthFilter, cancellationToken)).ToListAsync(cancellationToken);
        var trend = rows.GroupBy(item => item.MovementDate).OrderBy(group => group.Key).Select(group =>
        {
            var summary = Revenue(group);
            return new PortalDailyRevenue(group.Key, summary.GrossSales, summary.NetRevenue, summary.NegativeMovements);
        }).ToArray();
        return new(filter.StartDate!.Value, filter.EndDate!.Value, today, Revenue(rows), Revenue(monthRows),
            Revenue(monthRows.Where(item => item.MovementDate == today)), trend, await GetDataFreshnessAsync(cancellationToken));
    }

    public async Task<PortalPage<PortalSale>> GetSalesAsync(string seller, CommercialFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1 || page > 1_000_000) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize < 1 || pageSize > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var query = await QueryAsync(seller, filter, cancellationToken);
        var count = await query.CountAsync(cancellationToken);
        var rows = await Ordered(query).Skip((page - 1) * pageSize).Take(pageSize).Select(item => new PortalSale(
            item.Id, item.MovementDate, item.DocumentNumber, item.MovementType, item.CustomerCode,
            item.CustomerName, item.ProductName, item.Brand, item.Quantity, item.TotalValue)).ToListAsync(cancellationToken);
        return new(rows, page, pageSize, count);
    }

    public async Task<PortalCustomers> GetCustomersAsync(string seller, CommercialFilter filter, CancellationToken cancellationToken)
    {
        var rows = await (await QueryAsync(seller, filter, cancellationToken)).ToListAsync(cancellationToken);
        var customers = rows.GroupBy(CustomerKey, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Key.Length > 0 && group.Any(IsPurchase))
            .Select(Customer).OrderByDescending(item => item.NetRevenue).ThenBy(item => item.CustomerCode, StringComparer.Ordinal).ToArray();
        return new(true, customers.Take(ListLimit).ToArray(), customers.Length, customers.Length > ListLimit);
    }

    public async Task<PortalCustomerDetail?> GetCustomerAsync(string seller, string customerCode, CommercialFilter filter, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerCode);
        var code = customerCode.Trim().ToUpperInvariant();
        var query = (await QueryAsync(seller, filter, cancellationToken)).Where(item =>
            item.CustomerCode.Trim().ToUpper() == code ||
            (item.CustomerCode.Trim() == "" && item.CustomerName.Trim().ToUpper() == code));
        var rows = await Ordered(query).ToListAsync(cancellationToken);
        if (!rows.Any(IsPurchase)) return null;
        return new(Customer(rows), rows.Take(ListLimit).Select(Sale).ToArray(), rows.Count, rows.Count > ListLimit);
    }

    public Task<PortalRanking> GetProductsAsync(string seller, CommercialFilter filter, CancellationToken cancellationToken) =>
        RankingAsync(seller, filter, item => item.ProductName, cancellationToken);

    public Task<PortalRanking> GetBrandsAsync(string seller, CommercialFilter filter, CancellationToken cancellationToken) =>
        RankingAsync(seller, filter, item => item.Brand, cancellationToken);

    public async Task<PortalGoals> GetGoalsAsync(string seller, int year, int month, CancellationToken cancellationToken)
    {
        ValidateMonth(seller, year, month);
        var closing = await closingService.GetAsync(seller, year, month, cancellationToken);
        if (closing?.IsApproved == true)
        {
            if (closing.Monthly.Scope != "seller") return new(year, month, false, Unavailable, []);
            var approvedGoals = closing.BrandAwards.OrderBy(item => item.Brand).SelectMany(brand => new[]
            {
                Goal("FATURAMENTO", brand.RevenueGoal, brand.RevenueActual, brand.Brand, brand.RevenuePrize)
                    with { CurrentPrize = brand.RevenueGoal > 0 ? brand.RevenueAward : null },
                Goal("POSITIVACAO", brand.PositivityGoal, brand.PositivityActual, brand.Brand, brand.PositivityPrize)
                    with { CurrentPrize = brand.PositivityGoal > 0 ? brand.PositivityAward : null }
            }).ToArray();
            return new(year, month, true, null, approvedGoals) { IsApproved = true };
        }
        var names = SellerAliasCatalog.GetMatchingNames(seller);
        var duplicates = await ImportedBatchSelection.GetDuplicateIdsAsync(dbContext, cancellationToken);
        var goals = await dbContext.GoalRecords.AsNoTracking().Where(item => !duplicates.Contains(item.ImportBatchId)
            && names.Contains(item.Seller.Trim().ToUpper()) && item.Year == year && item.Month == month)
            .OrderBy(item => item.Description).ThenBy(item => item.GoalType).ToListAsync(cancellationToken);
        // Special supervisor/company closings must never supply commercial values to a personal query.
        var awards = closing?.Monthly.Scope == "seller" ? closing.BrandAwards : [];
        var items = goals.Where(goal => goal.GoalType is "FATURAMENTO" or "POSITIVACAO").Select(goal =>
        {
            var match = Regex.Match(goal.Description, @"^Marca\s+(.+?)\s*/", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var brand = match.Success ? match.Groups[1].Value.Trim() : goal.Description;
            var award = awards.FirstOrDefault(item => string.Equals(item.Brand, brand, StringComparison.OrdinalIgnoreCase));
            decimal? maximum = award is null ? null : goal.GoalType == "FATURAMENTO" ? award.RevenuePrize : award.PositivityPrize;
            return Goal(goal.GoalType, goal.Target, goal.Achieved, brand, maximum);
        }).ToArray();
        var available = closing?.Monthly.Scope == "seller" && items.All(item => item.MaximumPrize.HasValue);
        return new(year, month, available, available ? null : Unavailable, items);
    }

    public async Task<PortalPpp> GetPppAsync(string seller, int year, int month, CancellationToken cancellationToken)
    {
        ValidateMonth(seller, year, month);
        var closing = await closingService.GetAsync(seller, year, month, cancellationToken);
        if (closing?.IsApproved == true)
        {
            if (closing.Monthly.Scope != "seller") return new(year, month, false, Unavailable, null, null, []);
            var approvedSegments = closing.PppSegments.OrderBy(item => item.Segment).Select(item => new PortalPppSegment(
                item.Segment, item.CustomerCount, item.ItemsPerSegment, item.GroupsPlaced, item.AchievementPercent)).ToArray();
            return new(year, month, true, null,
                approvedSegments.Any(item => item.AchievementPercent.HasValue) ? closing.Ppp.MeanPercent : null,
                closing.Ppp.Award, approvedSegments) { IsApproved = true };
        }
        var names = SellerAliasCatalog.GetMatchingNames(seller);
        var duplicates = await ImportedBatchSelection.GetDuplicateIdsAsync(dbContext, cancellationToken);
        var rows = await dbContext.PppRecords.AsNoTracking().Where(item => !duplicates.Contains(item.ImportBatchId)
            && names.Contains(item.Seller.Trim().ToUpper()) && item.Year == year && item.Month == month)
            .OrderBy(item => item.Segment).ToListAsync(cancellationToken);
        var segments = rows.Select(item => new PortalPppSegment(item.Segment, item.CustomerCount, item.ItemsPerSegment, item.GroupsPlaced,
            item.CustomerCount > 0 && item.ItemsPerSegment > 0 ? item.GroupsPlaced / ((decimal)item.CustomerCount * item.ItemsPerSegment) * 100m : null)).ToArray();
        var available = closing?.Monthly.Scope == "seller";
        var calculation = PppCalculator.Calculate(0m, rows.Select(item => ((decimal)item.CustomerCount, (decimal)item.ItemsPerSegment, (decimal)item.GroupsPlaced)));
        return new(year, month, available, available ? null : Unavailable,
            segments.Any(item => item.AchievementPercent.HasValue) ? calculation.MeanPercent : null,
            available ? closing!.Ppp.Award : null, segments);
    }

    public async Task<PortalTrades> GetTradesAsync(string seller, CommercialFilter filter, CancellationToken cancellationToken)
    {
        var rows = await Ordered(await QueryAsync(seller, filter, cancellationToken)).ToListAsync(cancellationToken);
        var summary = TradeCalculator.Calculate(rows);
        var trades = rows.Where(item => item.MovementType is "TROCA" or "TROCA DEV").Take(ListLimit).Select(Sale).ToArray();
        return new(summary.PhysicalTrades, summary.TradeToSalesPercent, summary.TradeMovementCount, trades, summary.TradeMovementCount > ListLimit);
    }

    public async Task<PortalDataFreshness> GetDataFreshnessAsync(CancellationToken cancellationToken)
    {
        var sync = await dbContext.SynchronizationRuns.AsNoTracking()
            .Where(item => item.SourceSystem == "FIREBIRD" && item.Status == SynchronizationRunStatus.Completed && item.CompletedAtUtc != null)
            .OrderByDescending(item => item.CompletedAtUtc).Select(item => item.CompletedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (sync.HasValue) return new("firebird", sync, "sync-completed");
        var import = await dbContext.ImportBatches.AsNoTracking().Where(item => item.FileType == ImportFileType.Power &&
            (item.Status == ImportBatchStatus.Completed || item.Status == ImportBatchStatus.CompletedWithErrors))
            .OrderByDescending(item => item.StartedAtUtc).Select(item => (DateTimeOffset?)item.StartedAtUtc).FirstOrDefaultAsync(cancellationToken);
        // Legacy CSV batches store their start, not completion time. Preserve that distinction in the contract.
        return import.HasValue ? new("csv", import, "import-started") : new("unavailable", null, "unavailable");
    }

    private async Task<PortalRanking> RankingAsync(string seller, CommercialFilter filter, Func<CommercialMovement, string> key, CancellationToken cancellationToken)
    {
        var rows = await (await QueryAsync(seller, filter, cancellationToken)).ToListAsync(cancellationToken);
        var total = rows.Sum(item => item.TotalValue);
        var groups = rows.GroupBy(item => string.IsNullOrWhiteSpace(key(item)) ? "SEM INFORMACAO" : key(item).Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var summary = Revenue(group);
                return new PortalRankingItem(group.Key, summary.GrossSales, summary.NetRevenue, group.Sum(item => item.Quantity),
                    summary.MovementCount, summary.CustomerCount, total > 0 ? summary.NetRevenue / total * 100m : null);
            }).OrderByDescending(item => item.NetRevenue).ThenBy(item => item.Label, StringComparer.Ordinal).ToArray();
        return new(groups.Take(ListLimit).ToArray(), groups.Length, groups.Length > ListLimit);
    }

    private async Task<IQueryable<CommercialMovement>> QueryAsync(string seller, CommercialFilter filter, CancellationToken cancellationToken)
    {
        filter = NormalizeFilter(seller, filter);
        var duplicates = await ImportedBatchSelection.GetDuplicateIdsAsync(dbContext, cancellationToken);
        var query = CommercialMovementQuery.ApplyFilters(dbContext.CommercialMovements.AsNoTracking()
            .Where(item => !duplicates.Contains(item.ImportBatchId)), filter with { CustomerContains = null });
        if (!string.IsNullOrWhiteSpace(filter.CustomerContains))
        {
            var customer = filter.CustomerContains.Trim().ToUpperInvariant();
            query = query.Where(item => item.CustomerCode.ToUpper().Contains(customer) || item.CustomerName.ToUpper().Contains(customer));
        }
        return query;
    }

    private CommercialFilter NormalizeFilter(string seller, CommercialFilter filter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seller);
        var today = Today();
        var start = filter.StartDate ?? new DateOnly(today.Year, today.Month, 1);
        var end = filter.EndDate ?? new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        if (start > end) throw new ArgumentException("Data inicial deve ser anterior ou igual a data final.", nameof(filter));
        return filter with { Seller = seller, StartDate = start, EndDate = end };
    }

    private DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime((timeProvider ?? TimeProvider.System).GetUtcNow(), BrazilTimeZone).DateTime);
    private static void ValidateMonth(string seller, int year, int month)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seller);
        _ = new DateOnly(year, month, 1);
    }

    private static IOrderedQueryable<CommercialMovement> Ordered(IQueryable<CommercialMovement> query) => query
        .OrderByDescending(item => item.MovementDate).ThenBy(item => item.DocumentNumber).ThenBy(item => item.Id);
    private static bool IsPurchase(CommercialMovement item) => item.MovementType == "VENDA" && item.TotalValue > 0;
    private static string CustomerKey(CommercialMovement item) => string.IsNullOrWhiteSpace(item.CustomerCode) ? item.CustomerName.Trim() : item.CustomerCode.Trim();
    private static PortalSale Sale(CommercialMovement item) => new(item.Id, item.MovementDate, item.DocumentNumber,
        item.MovementType, item.CustomerCode, item.CustomerName, item.ProductName, item.Brand, item.Quantity, item.TotalValue);
    private static PortalCustomer Customer(IEnumerable<CommercialMovement> source)
    {
        var rows = source.ToArray();
        var latestPurchase = rows.Where(IsPurchase).OrderByDescending(item => item.MovementDate).ThenBy(item => item.Id).First();
        var summary = Revenue(rows);
        return new(CustomerKey(latestPurchase), latestPurchase.CustomerName, latestPurchase.City,
            summary.GrossSales, summary.NetRevenue, summary.DocumentCount, latestPurchase.MovementDate)
        {
            AverageTicket = summary.AverageTicket,
            PurchasedQuantity = summary.SaleQuantity
        };
    }

    private static PortalRevenueSummary Revenue(IEnumerable<CommercialMovement> rows)
    {
        var summary = DashboardCalculator.Calculate(rows);
        return new(summary.GrossSales, summary.NetResult, summary.NegativeMovements, summary.SaleQuantity,
            summary.MovementCount, summary.CustomerCount, summary.DocumentCount)
        {
            AverageTicket = summary.DocumentCount > 0 ? summary.NetResult / summary.DocumentCount : null
        };
    }

    private static PortalGoal Goal(string type, decimal target, decimal actual, string brand, decimal? maximum)
    {
        decimal? achieved = target > 0 ? actual / target * 100m : null;
        var revenue = type == "FATURAMENTO";
        decimal Payout(decimal percent, decimal prize) => revenue
            ? GoalPayoutCalculator.Revenue(percent, prize) : GoalPayoutCalculator.Positivity(percent, prize);
        decimal? current = maximum.HasValue && achieved.HasValue ? Payout(achieved.Value, maximum.Value) : null;
        decimal? next = !achieved.HasValue || achieved >= 100 ? null : !revenue ? 100m : achieved < 80 ? 80m : achieved < 90 ? 90m : 100m;
        return new(brand, type, target, actual, achieved, maximum, current, next,
            next.HasValue ? decimal.Max(0, target * next.Value / 100m - actual) : null,
            next.HasValue && maximum.HasValue ? Payout(next.Value, maximum.Value) : null);
    }
}
