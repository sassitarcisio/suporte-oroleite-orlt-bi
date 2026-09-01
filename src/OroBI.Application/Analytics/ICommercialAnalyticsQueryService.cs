namespace OroBI.Application.Analytics;

public interface ICommercialAnalyticsQueryService
{
    Task<TradeSummary> GetTradesAsync(CommercialFilter filter, CancellationToken cancellationToken);
    Task<SalesTradeSummary> GetSalesTradesAsync(CommercialFilter filter, CancellationToken cancellationToken);
    Task<MarginSummary> GetMarginsAsync(CommercialFilter filter, CancellationToken cancellationToken);
}
