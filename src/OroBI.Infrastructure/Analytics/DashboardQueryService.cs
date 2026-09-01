using Microsoft.EntityFrameworkCore;
using OroBI.Application.Analytics;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Analytics;

public sealed class DashboardQueryService(OroBiDbContext dbContext) : IDashboardQueryService, ICommercialAnalyticsQueryService
{
    public async Task<DashboardSummary> GetAsync(CommercialFilter filter, CancellationToken cancellationToken)
    {
        return DashboardCalculator.Calculate(await GetMovementsAsync(filter, cancellationToken));
    }

    public async Task<TradeSummary> GetTradesAsync(CommercialFilter filter, CancellationToken cancellationToken) =>
        TradeCalculator.Calculate(await GetMovementsAsync(filter, cancellationToken));

    public async Task<SalesTradeSummary> GetSalesTradesAsync(CommercialFilter filter, CancellationToken cancellationToken) =>
        SalesTradeCalculator.Calculate(await GetMovementsAsync(filter, cancellationToken));

    public async Task<MarginSummary> GetMarginsAsync(CommercialFilter filter, CancellationToken cancellationToken) =>
        MarginCalculator.Calculate(await GetMovementsAsync(filter, cancellationToken));

    private async Task<IEnumerable<OroBI.Domain.Commercial.CommercialMovement>> GetMovementsAsync(CommercialFilter filter, CancellationToken cancellationToken)
    {
        var movements = await dbContext.CommercialMovements.AsNoTracking().ToListAsync(cancellationToken);
        return CommercialFilters.Apply(movements, filter);
    }
}
