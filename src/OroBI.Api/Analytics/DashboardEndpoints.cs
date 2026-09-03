using OroBI.Application.Analytics;
using OroBI.Api.Auth;

namespace OroBI.Api.Analytics;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder endpoints, string prefix = "/api")
    {
        endpoints.MapGet($"{prefix}/dashboard", async (
            [AsParameters] DashboardQueryParameters query,
            IDashboardQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var summary = await queryService.GetAsync(query.ToCommercialFilter(), cancellationToken);
            return Results.Ok(summary);
        }).RequireAuthorization(AuthorizationPolicies.ManagerOrAdministrator);

        endpoints.MapGet($"{prefix}/dashboard/details", async (
            [AsParameters] DashboardQueryParameters query,
            IDashboardQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var details = await queryService.GetDetailsAsync(query.ToCommercialFilter(), cancellationToken);
            return Results.Ok(details);
        }).RequireAuthorization(AuthorizationPolicies.ManagerOrAdministrator);

        return endpoints;
    }
}
