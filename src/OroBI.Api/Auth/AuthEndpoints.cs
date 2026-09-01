using OroBI.Application.Identity;

namespace OroBI.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints, string prefix = "/api")
    {
        endpoints.MapPost($"{prefix}/auth/login", async (
            LoginRequest request,
            ILocalAuthenticationService authenticationService,
            CancellationToken cancellationToken) =>
        {
            var result = await authenticationService.LoginAsync(request.Email, request.Password, cancellationToken);
            if (result is null)
            {
                return Results.Unauthorized();
            }
            return Results.Ok(result);
        }).AllowAnonymous();

        return endpoints;
    }

    public sealed record LoginRequest(string Email, string Password);

}
