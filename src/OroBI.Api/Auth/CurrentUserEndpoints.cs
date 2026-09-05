using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OroBI.Application.Identity;
using OroBI.Domain.Sellers;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Api.Auth;

public static class CurrentUserEndpoints
{
    public static IEndpointRouteBuilder MapCurrentUserEndpoints(this IEndpointRouteBuilder endpoints, string prefix = "/api")
    {
        endpoints.MapGet($"{prefix}/me", async (ClaimsPrincipal user, IConfiguration configuration, IDataAccessScope scope, OroBiDbContext db, CancellationToken ct) =>
        {
            var access = await scope.ResolveAsync(user, null, ct);
            var id = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
            var ids = await db.UserSellerAccesses.AsNoTracking().Where(a => a.UserId == id && a.IsActive && a.Seller.IsActive).Select(a => a.SellerId).ToArrayAsync(ct);
            var accesses = new List<CurrentSellerAccess>();
            foreach (var sellerId in ids)
            {
                var available = await scope.ResolveAsync(user, sellerId, ct);
                if (available is not null) accesses.Add(new(available.SellerId, available.Name, available.Permissions));
            }
            return Results.Ok(new CurrentUserResponse(
            user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"),
            user.FindFirstValue(ClaimTypes.Email),
            user.Identity?.Name,
            user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray(),
            access?.Name,
            HasEntraConfiguration(configuration), access?.SellerId, access?.Permissions, access?.Name, accesses));
        })
            .RequireAuthorization();

        return endpoints;
    }

    private static bool HasEntraConfiguration(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Entra:ClientId"])
        && !string.IsNullOrWhiteSpace(configuration["Entra:TenantId"])
        && !string.IsNullOrWhiteSpace(configuration["Entra:ClientSecret"]);

    public sealed record CurrentUserResponse(
        string? UserId,
        string? Email,
        string? UserName,
        IReadOnlyCollection<string> Roles,
        string? Seller,
        bool EntraEnabled,
        Guid? SellerId = null,
        SellerPortalPermissions? Permissions = null,
        string? SellerName = null,
        IReadOnlyCollection<CurrentSellerAccess>? SellerAccesses = null);

    public sealed record CurrentSellerAccess(Guid SellerId, string Name, SellerPortalPermissions Permissions);
}
