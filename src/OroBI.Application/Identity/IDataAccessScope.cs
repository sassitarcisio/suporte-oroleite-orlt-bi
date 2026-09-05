using System.Security.Claims;
using OroBI.Domain.Sellers;

namespace OroBI.Application.Identity;

public interface IDataAccessScope
{
    Task<SellerAccess?> ResolveAsync(ClaimsPrincipal user, Guid? requestedSellerId, CancellationToken cancellationToken);
}

public sealed record SellerAccess(Guid SellerId, string Name, string ImportedName, SellerPortalPermissions Permissions);
