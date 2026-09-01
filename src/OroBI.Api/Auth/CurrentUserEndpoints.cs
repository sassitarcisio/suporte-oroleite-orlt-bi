using System.Security.Claims;

namespace OroBI.Api.Auth;

public static class CurrentUserEndpoints
{
    public static IEndpointRouteBuilder MapCurrentUserEndpoints(this IEndpointRouteBuilder endpoints, string prefix = "/api")
    {
        endpoints.MapGet($"{prefix}/me", (ClaimsPrincipal user, IConfiguration configuration) => Results.Ok(new CurrentUserResponse(
            user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"),
            user.FindFirstValue(ClaimTypes.Email),
            user.Identity?.Name,
            user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray(),
            user.FindFirstValue("seller"),
            HasEntraConfiguration(configuration))))
            .RequireAuthorization();

        return endpoints;
    }

    private static bool HasEntraConfiguration(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Entra:ClientId"])
        && !string.IsNullOrWhiteSpace(configuration["Entra:TenantId"])
        && !string.IsNullOrWhiteSpace(configuration["Entra:ClientSecret"]);

    public sealed record CurrentUserResponse(
        string? UserId,
        string? Email,
        string? UserName,
        IReadOnlyCollection<string> Roles,
        string? Seller,
        bool EntraEnabled);
}
