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

    public async Task<DashboardDetails> GetDetailsAsync(CommercialFilter filter, CancellationToken cancellationToken) =>
        DashboardCalculator.BuildDetails(await GetMovementsAsync(filter, cancellationToken));

    public async Task<TradeSummary> GetTradesAsync(CommercialFilter filter, CancellationToken cancellationToken) =>
        TradeCalculator.Calculate(await GetMovementsAsync(filter, cancellationToken));

    public async Task<SalesTradeSummary> GetSalesTradesAsync(CommercialFilter filter, CancellationToken cancellationToken) =>
        SalesTradeCalculator.Calculate(await GetMovementsAsync(filter, cancellationToken));

    public async Task<MarginSummary> GetMarginsAsync(CommercialFilter filter, CancellationToken cancellationToken) =>
        MarginCalculator.Calculate(await GetMovementsAsync(filter, cancellationToken));

    public async Task<NetMarginReport> GetNetMarginAsync(CommercialFilter filter, CancellationToken cancellationToken)
    {
        var movements = (await GetMovementsAsync(filter, cancellationToken)).ToArray();
        var grossSales = movements.Where(item => item.MovementType == "VENDA").Sum(item => item.TotalValue);
        var returns = movements.Where(item => item.MovementType is "DEVOL ENT" or "DEVOLUCAO").Sum(item => decimal.Abs(item.TotalValue));
        var netCost = movements.Where(item => item.MovementType == "VENDA").Sum(item => item.Quantity * item.UnitCost);
        var tradeLosses = movements.Where(item => item.MovementType is "TROCA" or "TROCA DEV").Sum(item => decimal.Abs(item.TotalValue));
        var boletoDiscounts = movements.Where(item => item.MovementType == "DESC BOLETO").Sum(item => decimal.Abs(item.TotalValue));
        var productCount = movements.Where(item => item.MovementType == "VENDA" && !string.IsNullOrWhiteSpace(item.ProductName))
            .Select(item => item.ProductName).Distinct(StringComparer.Ordinal).Count();
        return NetMarginReport.Create(grossSales, returns, netCost, tradeLosses, boletoDiscounts, productCount);
    }

    private async Task<IEnumerable<OroBI.Domain.Commercial.CommercialMovement>> GetMovementsAsync(CommercialFilter filter, CancellationToken cancellationToken)
    {
        var movements = await dbContext.CommercialMovements.AsNoTracking().ToListAsync(cancellationToken);
        return CommercialFilters.Apply(movements, filter);
    }
}
