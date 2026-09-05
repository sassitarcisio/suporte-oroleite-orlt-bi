using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OroBI.Application.Identity;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Identity;

public sealed class LocalAuthenticationService(
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    OroBiDbContext db) : ILocalAuthenticationService
{
    public async Task<LocalLoginResult?> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)) return null;

        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user))
        {
            await AuditAsync("LoginFailed", user?.Id, email, cancellationToken);
            return null;
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            await AuditAsync("LoginFailed", user.Id, email, cancellationToken);
            return null;
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            await AuditAsync("LoginFailed", user.Id, email, cancellationToken);
            return null;
        }
        var resetResult = await userManager.ResetAccessFailedCountAsync(user);
        if (!resetResult.Succeeded) return null;

        var roles = await userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new("session_version", await userManager.GetSecurityStampAsync(user)),
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

        await AuditAsync("LoginSucceeded", user.Id, email, cancellationToken);
        return new LocalLoginResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAt, roles.ToArray());
    }

    private async Task AuditAsync(string action, string? userId, string email, CancellationToken cancellationToken)
    {
        var target = userId ?? email.Trim().ToUpperInvariant();
        db.AccountAuditEvents.Add(new AccountAuditEvent
        {
            Action = action, ActorUserId = userId ?? string.Empty,
            TargetId = target.Length > 450 ? target[..450] : target
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
