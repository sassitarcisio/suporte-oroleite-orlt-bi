using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using OroBI.Application.Identity;

namespace OroBI.Api.Auth;

public sealed class BrowserSessionOptions
{
    public const string SectionName = "BrowserSession";
    public bool Enabled { get; set; }
    public string ApiHost { get; set; } = "api-bi.oroleite.com.br";
    // Defaults are supplied by appsettings, so binding an explicit allowlist never appends an implicit origin.
    public string[] AllowedOrigins { get; set; } = [];

    public bool IsValid() => !Enabled ||
        IsDnsHost(ApiHost) && AllowedOrigins.Length > 0 && AllowedOrigins.All(origin =>
            Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
            IsDnsHost(uri.Host) && string.IsNullOrEmpty(uri.UserInfo) && uri.GetLeftPart(UriPartial.Authority) == origin);

    private static bool IsDnsHost(string host) => !string.IsNullOrWhiteSpace(host) &&
        !host.Contains('*') && !host.EndsWith('.') && Uri.CheckHostName(host) == UriHostNameType.Dns;
}

public static class BrowserSession
{
    public const string CookieName = "__Host-OroBI.Session";
    public const string HeaderName = "X-OroBI-Session";

    public static bool UsesCookieTransport(HttpRequest request) => !request.Headers.ContainsKey("Authorization") &&
        (request.Headers.ContainsKey(HeaderName) || request.Cookies.ContainsKey(CookieName));

    public static bool IsAllowedOrigin(HttpRequest request, BrowserSessionOptions options) => options.Enabled &&
        request.Headers.Origin.Count == 1 && options.AllowedOrigins.Contains(request.Headers.Origin[0], StringComparer.Ordinal);

    public static bool IsEligible(HttpRequest request, BrowserSessionOptions options) =>
        options.Enabled && request.IsHttps && request.Host.Host.Equals(options.ApiHost, StringComparison.OrdinalIgnoreCase) &&
        IsAllowedOrigin(request, options) && request.Headers[HeaderName].Count == 1 && request.Headers[HeaderName][0] == "cookie";

    public static Task ReceiveTokenAsync(MessageReceivedContext context)
    {
        var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<BrowserSessionOptions>>().Value;
        if (UsesCookieTransport(context.Request) && IsEligible(context.Request, options))
            context.Token = context.Request.Cookies[CookieName];
        return Task.CompletedTask;
    }

    public static IResult SignIn(HttpContext context, LocalLoginResult login, bool rememberDevice = true)
    {
        var expiry = new DateTimeOffset(DateTime.SpecifyKind(login.ExpiresAtUtc, DateTimeKind.Utc));
        var maximum = DateTimeOffset.UtcNow.AddHours(8);
        if (expiry > maximum) expiry = maximum;
        // JWT exp and HTTP cookie dates have whole-second precision; return that same absolute deadline.
        expiry = DateTimeOffset.FromUnixTimeSeconds(expiry.ToUnixTimeSeconds());
        context.Response.Cookies.Append(CookieName, login.AccessToken, CookieOptions(rememberDevice ? expiry : null));
        return Results.Ok(new { sessionMode = "cookie", expiresAtUtc = expiry, login.Roles, login.MustChangePassword });
    }

    public static void Clear(HttpContext context)
    {
        if (UsesCookieTransport(context.Request)) context.Response.Cookies.Delete(CookieName, CookieOptions(null));
    }

    public static DateTimeOffset? ExpiresAtUtc(ClaimsPrincipal principal) =>
        long.TryParse(principal.FindFirstValue("exp"), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            && seconds is >= 0 and <= 253402300799 ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null;

    private static CookieOptions CookieOptions(DateTimeOffset? expiry) => new()
    {
        HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/", IsEssential = true, Expires = expiry
    };
}

public sealed class BrowserSessionGuardMiddleware(RequestDelegate next, IOptions<BrowserSessionOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) &&
            BrowserSession.UsesCookieTransport(context.Request) && !BrowserSession.IsEligible(context.Request, options.Value))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { code = "cookie_session_forbidden", error = "A sessão do navegador não está disponível para esta origem." });
            return;
        }
        await next(context);
    }
}
