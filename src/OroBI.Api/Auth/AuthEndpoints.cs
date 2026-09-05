using System.Globalization;
using System.Threading.RateLimiting;
using OroBI.Application.Identity;

namespace OroBI.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints, string prefix = "/api")
    {
        endpoints.MapPost($"{prefix}/auth/login", async (
            LoginRequest request,
            ILocalAuthenticationService authenticationService,
            LoginRateLimiter rateLimiter,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            using var lease = rateLimiter.TryAcquire(request.Email);
            if (!lease.IsAcquired)
            {
                var retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var retry) ? retry : TimeSpan.FromMinutes(1);
                context.Response.Headers.RetryAfter = Math.Max(1, Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            }
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
