using OroBI.Application.Analytics;

namespace OroBI.Application.Portal;

/// <summary>Every seller argument must be the imported name resolved by the server access scope.</summary>
public interface IPortalQueryService
{
    Task<PortalDashboard> GetDashboardAsync(string seller, CommercialFilter filter, CancellationToken cancellationToken);
    Task<PortalPage<PortalSale>> GetSalesAsync(string seller, CommercialFilter filter, int page, int pageSize, CancellationToken cancellationToken);
    Task<PortalCustomers> GetCustomersAsync(string seller, CommercialFilter filter, CancellationToken cancellationToken);
    Task<PortalCustomerDetail?> GetCustomerAsync(string seller, string customerCode, CommercialFilter filter, CancellationToken cancellationToken);
    Task<PortalRanking> GetProductsAsync(string seller, CommercialFilter filter, CancellationToken cancellationToken);
    Task<PortalRanking> GetBrandsAsync(string seller, CommercialFilter filter, CancellationToken cancellationToken);
    Task<PortalGoals> GetGoalsAsync(string seller, int year, int month, CancellationToken cancellationToken);
    Task<PortalPpp> GetPppAsync(string seller, int year, int month, CancellationToken cancellationToken);
    Task<PortalTrades> GetTradesAsync(string seller, CommercialFilter filter, CancellationToken cancellationToken);
    Task<PortalDataFreshness> GetDataFreshnessAsync(CancellationToken cancellationToken);
}
