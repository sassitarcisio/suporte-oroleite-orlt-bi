using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using OroBI.Infrastructure.Identity;

namespace OroBI.Api.Auth;

public static class SessionTokenValidation
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var id = principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal?.FindFirstValue("sub");
        var version = principal?.FindFirstValue("session_version");
        var manager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = id is null ? null : await manager.FindByIdAsync(id);
        if (user is null || !user.IsActive || string.IsNullOrEmpty(version) || version != await manager.GetSecurityStampAsync(user))
        {
            context.Fail("Session is no longer valid.");
            return;
        }
        // Every request observes persisted roles; a signed but stale role never retains authority.
        var identity = new ClaimsIdentity(context.Scheme.Name, ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new(ClaimTypes.NameIdentifier, user.Id));
        identity.AddClaim(new(ClaimTypes.Name, user.UserName ?? string.Empty));
        identity.AddClaim(new(ClaimTypes.Email, user.Email ?? string.Empty));
        identity.AddClaim(new("session_version", version));
        identity.AddClaims((await manager.GetRolesAsync(user)).Select(role => new Claim(ClaimTypes.Role, role)));
        context.Principal = new ClaimsPrincipal(identity);
    }
}
