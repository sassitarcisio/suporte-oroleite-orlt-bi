using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OroBI.Application.Identity;

namespace OroBI.Infrastructure.Identity;

public sealed class LocalAuthenticationService(
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration) : ILocalAuthenticationService
{
    public async Task<LocalLoginResult?> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)) return null;

        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            return null;
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            return null;
        }

        if (await userManager.IsLockedOutAsync(user)) return null;
        var resetResult = await userManager.ResetAccessFailedCountAsync(user);
        if (!resetResult.Succeeded) return null;

        var roles = await userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? user.UserName ?? string.Empty),
            new(ClaimTypes.Name, user.UserName ?? string.Empty)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        if (!string.IsNullOrWhiteSpace(user.Seller))
        {
            claims.Add(new Claim("seller", user.Seller));
        }

        var issuer = configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT issuer is required.");
        var audience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT audience is required.");
        var signingKey = configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("JWT signing key is required.");
        var expiresAt = DateTime.UtcNow.AddHours(8);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256));

        return new LocalLoginResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAt, roles.ToArray());
    }
}
