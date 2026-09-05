using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OroBI.Application.Identity;
using OroBI.Domain.Sellers;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Identity;

public sealed class DataAccessScope(OroBiDbContext db) : IDataAccessScope
{
    public async Task<SellerAccess?> ResolveAsync(ClaimsPrincipal user, Guid? requestedSellerId, CancellationToken cancellationToken)
    {
        if (user.Identity?.IsAuthenticated != true) return null;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        if (userId is null || !await db.Users.AnyAsync(u => u.Id == userId && u.IsActive, cancellationToken)) return null;
        var roles = await (from link in db.UserRoles join role in db.Roles on link.RoleId equals role.Id
                           where link.UserId == userId select role.Name).ToArrayAsync(cancellationToken);
        if (roles.Contains("Administrador") || roles.Contains("Diretoria"))
        {
            if (requestedSellerId is null) return null;
            var seller = await db.Sellers.AsNoTracking().SingleOrDefaultAsync(s => s.Id == requestedSellerId, cancellationToken);
            return seller is null ? null : new(seller.Id, seller.Name, seller.ImportedName, new SellerPortalPermissions());
        }
        var isSeller = roles.Contains("Vendedor");
        if (!isSeller && !roles.Contains("Gestor") && !roles.Contains("Gerente")) return null;
        var accesses = await db.UserSellerAccesses.AsNoTracking().Include(a => a.Seller)
            .Where(a => a.UserId == userId && a.IsActive && a.Seller.IsActive).ToArrayAsync(cancellationToken);
        if (isSeller && accesses.Length != 1) return null;
        var access = requestedSellerId is null
            ? (accesses.Length == 1 ? accesses[0] : null)
            : accesses.SingleOrDefault(a => a.SellerId == requestedSellerId);
        return access is null ? null : new(access.SellerId, access.Seller.Name, access.Seller.ImportedName, access.Permissions);
    }
}

public static class SellerPortalIdentityRegistration
{
    public static IServiceCollection AddSellerPortalIdentity(this IServiceCollection services)
    {
        services.AddScoped<IDataAccessScope, DataAccessScope>();
        new Microsoft.AspNetCore.Identity.IdentityBuilder(typeof(ApplicationUser), typeof(Microsoft.AspNetCore.Identity.IdentityRole), services)
            .AddDefaultTokenProviders();
        return services;
    }
}
