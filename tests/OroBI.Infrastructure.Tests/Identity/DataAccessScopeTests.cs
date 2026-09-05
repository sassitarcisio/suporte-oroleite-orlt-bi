using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OroBI.Domain.Sellers;
using OroBI.Infrastructure.Identity;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Tests.Identity;

public sealed class DataAccessScopeTests
{
    [Theory]
    [InlineData("Vendedor")]
    [InlineData("Gestor")]
    [InlineData("Gerente")]
    public async Task Persisted_links_allow_only_assigned_seller_and_apply_permissions(string role)
    {
        await using var db = CreateDb();
        var own = new Seller { Name = "Ana", ImportedName = "ANA" };
        var other = new Seller { Name = "Bia", ImportedName = "BIA" };
        db.AddRange(own, other);
        db.Users.Add(new ApplicationUser { Id = "user" });
        db.Roles.Add(new IdentityRole(role) { Id = "role" });
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = "user", RoleId = "role" });
        db.UserSellerAccesses.Add(new UserSellerAccess { UserId = "user", SellerId = own.Id, Permissions = new() { CanViewRevenue = false } });
        await db.SaveChangesAsync();
        var scope = new DataAccessScope(db);
        var principal = Principal(role);
        Assert.Null(await scope.ResolveAsync(principal, other.Id, default));
        var access = await scope.ResolveAsync(principal, own.Id, default);
        Assert.NotNull(access);
        Assert.False(access.Permissions.CanViewRevenue);
        own.IsActive = false;
        await db.SaveChangesAsync();
        Assert.Null(await scope.ResolveAsync(principal, own.Id, default));
    }

    [Fact]
    public async Task Forged_name_and_role_claims_never_grant_scope()
    {
        await using var db = CreateDb();
        var seller = new Seller { Name = "Ana", ImportedName = "ANA" };
        db.Add(seller);
        db.Users.Add(new ApplicationUser { Id = "user" });
        await db.SaveChangesAsync();
        Assert.Null(await new DataAccessScope(db).ResolveAsync(Principal("Administrador"), seller.Id, default));
    }

    [Fact]
    public async Task Seller_with_ambiguous_links_is_denied_even_when_request_selects_one()
    {
        await using var db = CreateDb();
        var sellers = new[] { new Seller { Name = "Ana", ImportedName = "ANA" }, new Seller { Name = "Bia", ImportedName = "BIA" } };
        db.AddRange(sellers);
        db.Users.Add(new ApplicationUser { Id = "user" });
        db.Roles.Add(new IdentityRole("Vendedor") { Id = "role" });
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = "user", RoleId = "role" });
        db.UserSellerAccesses.AddRange(sellers.Select(s => new UserSellerAccess { UserId = "user", SellerId = s.Id }));
        await db.SaveChangesAsync();
        Assert.Null(await new DataAccessScope(db).ResolveAsync(Principal("Vendedor"), sellers[0].Id, default));
    }

    private static OroBiDbContext CreateDb() => new(new DbContextOptionsBuilder<OroBiDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static ClaimsPrincipal Principal(string role) => new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "user"), new(ClaimTypes.Role, role), new("seller", "ANA")], "test"));
}
