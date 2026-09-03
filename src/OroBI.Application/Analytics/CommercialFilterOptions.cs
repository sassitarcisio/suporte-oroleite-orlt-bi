namespace OroBI.Application.Analytics;

public sealed record CommercialFilterOptions(string[] Brands, string[] Groups, string[] Cities, string[] MovementTypes);

public interface ICommercialFilterOptionsQueryService
{
    Task<CommercialFilterOptions> GetAsync(CancellationToken cancellationToken);
}
