using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OroBI.Infrastructure.Identity;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Tests.Identity;

public sealed class InitialAdminProvisionerTests
{
    [Fact]
    public async Task ProvisionAsync_creates_administrators_and_is_idempotent()
    {
        await using var context = new OroBiDbContext(
            new DbContextOptionsBuilder<OroBiDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var userStore = new UserStore<ApplicationUser, IdentityRole, OroBiDbContext>(context);
        var roleStore = new RoleStore<IdentityRole, OroBiDbContext>(context);
        var userManager = new UserManager<ApplicationUser>(
            userStore,
            null!,
            new PasswordHasher<ApplicationUser>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            null!);
        var roleManager = new RoleManager<IdentityRole>(
            roleStore,
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!);
        var provisioner = new InitialAdminProvisioner(userManager, roleManager);
        var administrators = new[]
        {
            new InitialAdminCredential("tarcisio.sassi@oroleite.com.br", "StrongPassword123!"),
            new InitialAdminCredential("jeferson@oroleite.com.br", "AnotherStrongPassword123!")
        };

        await provisioner.ProvisionAsync(administrators, CancellationToken.None);
        await provisioner.ProvisionAsync(administrators, CancellationToken.None);

        Assert.Equal(2, await context.Users.CountAsync());
        Assert.True(await roleManager.RoleExistsAsync(InitialAdminProvisioner.AdministratorRole));
        foreach (var administrator in administrators)
        {
            var user = await userManager.FindByEmailAsync(administrator.Email);
            Assert.NotNull(user);
            Assert.True(await userManager.IsInRoleAsync(user, InitialAdminProvisioner.AdministratorRole));
            Assert.True(await userManager.CheckPasswordAsync(user, administrator.Password));
        }
    }
}
