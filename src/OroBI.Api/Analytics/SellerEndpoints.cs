using OroBI.Api.Auth;

namespace OroBI.Api.Analytics;

public static class SellerEndpoints
{
    public static IEndpointRouteBuilder MapSellerEndpoints(this IEndpointRouteBuilder endpoints, string prefix = "/api")
    {
        endpoints.MapGet($"{prefix}/sellers", () =>
        {
            return Results.Ok(SellerCatalog.Names);
        }).RequireAuthorization(AuthorizationPolicies.ManagerOrAdministrator);

        return endpoints;
    }
}
