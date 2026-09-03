namespace OroBI.Application.Analytics;

public interface IDashboardQueryService
{
    Task<DashboardSummary> GetAsync(CommercialFilter filter, CancellationToken cancellationToken);
    Task<DashboardDetails> GetDetailsAsync(CommercialFilter filter, CancellationToken cancellationToken);
}
