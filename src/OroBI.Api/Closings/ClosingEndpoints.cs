using OroBI.Api.Auth;
using Microsoft.EntityFrameworkCore;
using OroBI.Application.Closings;
using OroBI.Domain.Closings;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Api.Closings;

public static class ClosingEndpoints
{
    public static IEndpointRouteBuilder MapClosingEndpoints(this IEndpointRouteBuilder endpoints, string prefix = "/api")
    {
        endpoints.MapPost($"{prefix}/closing-configurations", async (ClosingConfigurationRequest request, OroBiDbContext dbContext, CancellationToken cancellationToken) =>
        {
            if (request.Month is < 1 or > 12) return Results.BadRequest(new { error = "month must be between 1 and 12." });
            var exists = await dbContext.SellerClosingConfigurations.AnyAsync(item => item.Seller == request.Seller.Trim().ToUpperInvariant() && item.Year == request.Year && item.Month == request.Month, cancellationToken);
            if (exists) return Results.Conflict(new { error = "A configuration already exists for this seller and month." });
            var configuration = SellerClosingConfiguration.Create(request.Seller, request.Year, request.Month, request.BaseSalary, request.CommissionPercent, request.PppMaximumAward);
            dbContext.SellerClosingConfigurations.Add(configuration);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Created($"{prefix}/closing-configurations/{configuration.Id}", new { configuration.Id });
        }).RequireAuthorization(AuthorizationPolicies.AdministratorOnly);
        endpoints.MapGet($"{prefix}/closings", async (string seller, string month, ISellerClosingQueryService service, CancellationToken cancellationToken) =>
        {
            if (!DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", out var period)) return Results.BadRequest(new { error = "month must use yyyy-MM." });
            var result = await service.GetAsync(seller, period.Year, period.Month, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization(AuthorizationPolicies.SellerScope);
        return endpoints;
    }

    public sealed record ClosingConfigurationRequest(string Seller, int Year, int Month, decimal BaseSalary, decimal CommissionPercent, decimal PppMaximumAward);
}
