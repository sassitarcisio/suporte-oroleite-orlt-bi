using Microsoft.AspNetCore.Identity;

namespace OroBI.Infrastructure.Identity;

public sealed record InitialAdminCredential(string Email, string Password);

public sealed class InitialAdminProvisioner(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager)
{
    public const string AdministratorRole = "Administrador";

    public async Task ProvisionAsync(
        IEnumerable<InitialAdminCredential> administrators,
        CancellationToken cancellationToken)
    {
        var credentials = administrators.ToArray();
        if (credentials.Length == 0 || credentials.Any(credential =>
                string.IsNullOrWhiteSpace(credential.Email) || string.IsNullOrWhiteSpace(credential.Password)))
        {
            throw new InvalidOperationException("Initial administrator email and password are required.");
        }

        if (!await roleManager.RoleExistsAsync(AdministratorRole))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole(AdministratorRole));
            EnsureSucceeded(roleResult);
        }

        foreach (var credential in credentials)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = await userManager.FindByEmailAsync(credential.Email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = credential.Email,
                    Email = credential.Email,
                    EmailConfirmed = true
                };
                var createResult = await userManager.CreateAsync(user, credential.Password);
                EnsureSucceeded(createResult);
            }

            if (!await userManager.IsInRoleAsync(user, AdministratorRole))
            {
                var roleResult = await userManager.AddToRoleAsync(user, AdministratorRole);
                EnsureSucceeded(roleResult);
            }
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}
