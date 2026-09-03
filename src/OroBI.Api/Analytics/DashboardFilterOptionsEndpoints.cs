using OroBI.Api.Auth;
using OroBI.Application.Analytics;

namespace OroBI.Api.Analytics;

public static class DashboardFilterOptionsEndpoints
{
    public static IEndpointRouteBuilder MapDashboardFilterOptionsEndpoints(this IEndpointRouteBuilder endpoints, string prefix = "/api")
    {
        endpoints.MapGet($"{prefix}/dashboard/filter-options", async (ICommercialFilterOptionsQueryService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAsync(cancellationToken))).RequireAuthorization(AuthorizationPolicies.ManagerOrAdministrator);

        return endpoints;
    }
}
