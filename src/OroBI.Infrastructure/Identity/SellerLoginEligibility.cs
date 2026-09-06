using Microsoft.EntityFrameworkCore;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Identity;

public static class SellerLoginEligibility
{
    public static async Task<bool> IsEligibleAsync(ApplicationUser user, IEnumerable<string> roles, OroBiDbContext db, CancellationToken ct)
    {
        if (!user.IsActive || user.IsRegistrationPending) return false;
        var persistedRoles = roles.ToArray();
        if (persistedRoles.Contains("Administrador") || persistedRoles.Contains("Diretoria")) return true;
        if (!persistedRoles.Contains("Vendedor")) return true;
        var links = await db.UserSellerAccesses.AsNoTracking()
            .Where(access => access.UserId == user.Id && access.IsActive)
            .Select(access => access.Seller.IsActive).ToArrayAsync(ct);
        return links.Length == 1 && links[0];
    }
}
