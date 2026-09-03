using Microsoft.EntityFrameworkCore;
using OroBI.Api.Auth;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Api.Analytics;

public static class SellerEndpoints
{
    public static IEndpointRouteBuilder MapSellerEndpoints(this IEndpointRouteBuilder endpoints, string prefix = "/api")
    {
        endpoints.MapGet($"{prefix}/sellers", async (OroBiDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var movementSellers = await dbContext.CommercialMovements
                .Select(movement => movement.Seller)
                .ToListAsync(cancellationToken);
            var closingSellers = await dbContext.SellerClosingConfigurations
                .Select(configuration => configuration.Seller)
                .ToListAsync(cancellationToken);
            var registeredSellers = await dbContext.Users
                .Where(user => user.Seller != null)
                .Select(user => user.Seller!)
                .ToListAsync(cancellationToken);

            var sellers = movementSellers
                .Concat(closingSellers)
                .Concat(registeredSellers)
                .Select(seller => seller.Trim())
                .Where(seller => !string.IsNullOrWhiteSpace(seller))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            return Results.Ok(sellers);
        }).RequireAuthorization(AuthorizationPolicies.ManagerOrAdministrator);

        return endpoints;
    }
}
