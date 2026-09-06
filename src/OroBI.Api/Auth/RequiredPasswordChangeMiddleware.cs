namespace OroBI.Api.Auth;

public sealed class RequiredPasswordChangeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true && context.User.HasClaim("must_change_password", "true")
            && context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            var path = (context.Request.Path.Value ?? string.Empty).TrimEnd('/');
            if (path.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase)) path = path[7..];
            else path = path[4..];
            var allowed = HttpMethods.IsGet(context.Request.Method) && path.Equals("/me", StringComparison.OrdinalIgnoreCase)
                || HttpMethods.IsPost(context.Request.Method) && (path.Equals("/me/change-password", StringComparison.OrdinalIgnoreCase)
                    || path.Equals("/auth/logout", StringComparison.OrdinalIgnoreCase));
            if (!allowed)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { code = "password_change_required", error = "Crie uma nova senha para continuar." });
                return;
            }
        }
        await next(context);
    }
}
