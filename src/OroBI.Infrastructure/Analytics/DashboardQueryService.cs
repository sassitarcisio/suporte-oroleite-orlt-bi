using Microsoft.EntityFrameworkCore;
using OroBI.Application.Analytics;
using OroBI.Infrastructure.Persistence;
using OroBI.Infrastructure.Imports;

namespace OroBI.Infrastructure.Analytics;

public sealed class DashboardQueryService(OroBiDbContext dbContext) : IDashboardQueryService, ICommercialAnalyticsQueryService
{
    public async Task<DashboardSummary> GetAsync(CommercialFilter filter, CancellationToken cancellationToken)
    {
        return DashboardCalculator.Calculate(await GetMovementsAsync(filter, cancellationToken));
    }

    public async Task<DashboardDetails> GetDetailsAsync(CommercialFilter filter, CancellationToken cancellationToken) =>
        DashboardCalculator.BuildDetails(await GetMovementsAsync(filter, cancellationToken));

    public async Task<TradeSummary> GetTradesAsync(CommercialFilter filter, CancellationToken cancellationToken) =>
        TradeCalculator.Calculate(await GetMovementsAsync(filter, cancellationToken));

    public async Task<TradeAnalysisReport> GetTradeAnalysisAsync(CommercialFilter filter, CancellationToken cancellationToken) =>
        TradeAnalysisCalculator.Calculate(await GetMovementsAsync(filter, cancellationToken));

    public async Task<SalesTradeSummary> GetSalesTradesAsync(CommercialFilter filter, CancellationToken cancellationToken) =>
        SalesTradeCalculator.Calculate(await GetMovementsAsync(filter, cancellationToken));

    public async Task<MarginSummary> GetMarginsAsync(CommercialFilter filter, CancellationToken cancellationToken) =>
        MarginCalculator.Calculate(await GetMovementsAsync(filter, cancellationToken));

    public async Task<NetMarginReport> GetNetMarginAsync(CommercialFilter filter, CancellationToken cancellationToken) =>
        NetMarginCalculator.Calculate(await GetMovementsAsync(filter, cancellationToken));

    private async Task<IEnumerable<OroBI.Domain.Commercial.CommercialMovement>> GetMovementsAsync(CommercialFilter filter, CancellationToken cancellationToken)
    {
        var duplicates = await ImportedBatchSelection.GetDuplicateIdsAsync(dbContext, cancellationToken);
        var query = dbContext.CommercialMovements.AsNoTracking()
            .Where(item => !duplicates.Contains(item.ImportBatchId));
        var movements = await CommercialMovementQuery.ApplyFilters(query, filter).ToListAsync(cancellationToken);
        // Keep the exact in-memory OrdinalIgnoreCase check on the filtered result.
        // Database case folding follows its collation, while this contract is ordinal.
        return CommercialFilters.Apply(movements, filter);
    }
}
