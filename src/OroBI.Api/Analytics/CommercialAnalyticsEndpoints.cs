using OroBI.Application.Analytics;
using OroBI.Api.Auth;

namespace OroBI.Api.Analytics;

public static class CommercialAnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapCommercialAnalyticsEndpoints(this IEndpointRouteBuilder endpoints, string prefix = "/api")
    {
        endpoints.MapGet($"{prefix}/trades", async ([AsParameters] DashboardQueryParameters query, ICommercialAnalyticsQueryService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetTradesAsync(query.ToCommercialFilter(), cancellationToken))).RequireAuthorization(AuthorizationPolicies.ManagerOrAdministrator);
        endpoints.MapGet($"{prefix}/sales-trades", async ([AsParameters] DashboardQueryParameters query, ICommercialAnalyticsQueryService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetSalesTradesAsync(query.ToCommercialFilter(), cancellationToken))).RequireAuthorization(AuthorizationPolicies.ManagerOrAdministrator);
        endpoints.MapGet($"{prefix}/margins", async ([AsParameters] DashboardQueryParameters query, ICommercialAnalyticsQueryService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetMarginsAsync(query.ToCommercialFilter(), cancellationToken))).RequireAuthorization(AuthorizationPolicies.ManagerOrAdministrator);
        endpoints.MapGet($"{prefix}/net-margin", async ([AsParameters] DashboardQueryParameters query, ICommercialAnalyticsQueryService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetNetMarginAsync(query.ToCommercialFilter(), cancellationToken))).RequireAuthorization(AuthorizationPolicies.ManagerOrAdministrator);
        return endpoints;
    }
}
