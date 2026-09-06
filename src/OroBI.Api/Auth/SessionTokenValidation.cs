using System.Security.Claims;
using System.Globalization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using OroBI.Infrastructure.Identity;
using OroBI.Infrastructure.Persistence;

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
        var roles = await manager.GetRolesAsync(user);
        var db = context.HttpContext.RequestServices.GetRequiredService<OroBiDbContext>();
        if (!await SellerLoginEligibility.IsEligibleAsync(user, roles, db, context.HttpContext.RequestAborted))
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
        identity.AddClaim(new("exp", new DateTimeOffset(DateTime.SpecifyKind(context.SecurityToken.ValidTo, DateTimeKind.Utc)).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)));
        identity.AddClaim(new("must_change_password", user.MustChangePassword ? "true" : "false"));
        identity.AddClaims(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        context.Principal = new ClaimsPrincipal(identity);
    }
}
