using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OroBI.Infrastructure.Identity;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Api.IntegrationTests.Auth;

public sealed partial class PortalSessionTests
{
    private const string CookieName = "__Host-OroBI.Session";
    private const string CorporateOrigin = "https://portal-bi.oroleite.com.br";
    private const string CorporateApi = "https://api-bi.oroleite.com.br";
    private const string CookieSigningKey = "synthetic-cookie-test-signing-key-at-least-thirty-two-characters";

    [Theory]
    [InlineData("/api", false)]
    [InlineData("/api/v1", false)]
    [InlineData("/api", true)]
    [InlineData("/api/v1", true)]
    public async Task Cookie_login_honors_remember_device_without_changing_signed_deadline_or_identity(string prefix, bool rememberDevice)
    {
        await using var factory = CreateCookieFactory();
        var id = await SeedAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OroBiDbContext>();
            (await db.Users.SingleAsync(user => user.Id == id)).MustChangePassword = true;
            await db.SaveChangesAsync();
        }
        using var client = CreateCookieClient(factory);
        var response = await client.PostAsJsonAsync(prefix + "/auth/login", new { email = "seller@example.invalid", password = "Synthetic-123!", rememberDevice });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Equal(rememberDevice, cookie.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("max-age=", cookie, StringComparison.OrdinalIgnoreCase);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.TryGetProperty("accessToken", out _));
        Assert.Equal("cookie", body.GetProperty("sessionMode").GetString());
        Assert.Equal("Vendedor", body.GetProperty("roles")[0].GetString());
        Assert.True(body.GetProperty("mustChangePassword").GetBoolean());
        var expires = body.GetProperty("expiresAtUtc").GetDateTimeOffset();
        var token = new JwtSecurityTokenHandler().ReadJwtToken(cookie.Split(';')[0][(CookieName.Length + 1)..]);
        Assert.Equal(token.ValidTo, expires.UtcDateTime);
        SetCookie(client, response);
        var me = await client.GetFromJsonAsync<JsonElement>(prefix + "/me");
        Assert.Equal(expires, me.GetProperty("expiresAtUtc").GetDateTimeOffset());
        Assert.True(me.GetProperty("mustChangePassword").GetBoolean());
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/v1")]
    public async Task Cookie_login_sets_host_only_secure_persistent_cookie_without_disclosing_token(string prefix)
    {
        await using var factory = CreateCookieFactory();
        await SeedAsync(factory);
        using var client = CreateCookieClient(factory);
        var tokenOptions = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>>().Get("Bearer");
        Assert.True(((SymmetricSecurityKey)tokenOptions.TokenValidationParameters.IssuerSigningKey).Key.SequenceEqual(Encoding.UTF8.GetBytes(CookieSigningKey)), "JWT issuance and validation must observe the same configured key.");
        var started = DateTimeOffset.UtcNow;
        var response = await CookieLoginAsync(client, prefix);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("cookie", body.GetProperty("sessionMode").GetString());
        Assert.False(body.TryGetProperty("accessToken", out _));
        Assert.False(body.GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal("Vendedor", body.GetProperty("roles")[0].GetString());
        var expires = body.GetProperty("expiresAtUtc").GetDateTimeOffset();
        Assert.InRange(expires, started.AddHours(8).AddSeconds(-2), DateTimeOffset.UtcNow.AddHours(8));
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith(CookieName + "=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        SetCookie(client, response);
        var me = await client.GetFromJsonAsync<JsonElement>(prefix + "/me");
        Assert.Equal("seller@example.invalid", me.GetProperty("email").GetString());
        Assert.Equal(expires.ToUnixTimeSeconds(), me.GetProperty("expiresAtUtc").GetDateTimeOffset().ToUnixTimeSeconds());
        var dashboard = await client.GetAsync(prefix + "/me/dashboard");
        if (prefix == "/api/v1") Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        Assert.False(dashboard.Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [InlineData(null, "cookie", CorporateApi)]
    [InlineData("null", "cookie", CorporateApi)]
    [InlineData("https://bi.oroleite.com.br", "cookie", CorporateApi)]
    [InlineData("https://evil.oroleite.com.br", "cookie", CorporateApi)]
    [InlineData("https://portal-bi.oroleite.com.br.evil.invalid", "cookie", CorporateApi)]
    [InlineData("https://portal-bi.oroleite.com.br/", "cookie", CorporateApi)]
    [InlineData("https://portal-bi.oroleite.com.br:444", "cookie", CorporateApi)]
    [InlineData("http://portal-bi.oroleite.com.br", "cookie", CorporateApi)]
    [InlineData(CorporateOrigin, "wrong", CorporateApi)]
    [InlineData(CorporateOrigin, "cookie", "https://evil.oroleite.com.br")]
    [InlineData(CorporateOrigin, "cookie", "http://api-bi.oroleite.com.br")]
    public async Task Cookie_login_rejects_ineligible_requests_before_any_identity_side_effect(string? origin, string header, string address)
    {
        await using var factory = CreateCookieFactory();
        var id = await SeedAsync(factory);
        using var client = CreateCookieClient(factory, origin, header, address);
        var response = await CookieLoginAsync(client);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OroBiDbContext>();
        Assert.Empty(await db.AccountAuditEvents.ToArrayAsync());
        Assert.Null((await db.Users.SingleAsync(x => x.Id == id)).LastLoginAtUtc);
    }

    [Theory]
    [InlineData(null, "cookie", "GET", "/api/v1/me")]
    [InlineData("null", "cookie", "POST", "/api/v1/auth/logout")]
    [InlineData("https://evil.oroleite.com.br", "cookie", "POST", "/api/me/change-password")]
    [InlineData(CorporateOrigin, null, "GET", "/api/me")]
    [InlineData(CorporateOrigin, null, "POST", "/api/v1/auth/login")]
    [InlineData(CorporateOrigin, null, "POST", "/api/v1/auth/logout")]
    public async Task Ambient_cookie_cannot_bypass_origin_and_custom_header_guard(string? origin, string? header, string method, string endpoint)
    {
        await using var factory = CreateCookieFactory();
        var id = await SeedAsync(factory);
        using var trusted = CreateCookieClient(factory);
        var login = await CookieLoginAsync(trusted);
        using var client = CreateCookieClient(factory, origin, header);
        SetCookie(client, login);
        using var request = new HttpRequestMessage(new HttpMethod(method), endpoint);
        request.Content = JsonContent.Create(new { email = "seller@example.invalid", password = "Synthetic-123!", currentPassword = "Synthetic-123!", newPassword = "Changed-456!" });
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OroBiDbContext>();
        Assert.Equal("LoginSucceeded", Assert.Single(await db.AccountAuditEvents.ToArrayAsync()).Action);
        Assert.NotNull((await db.Users.SingleAsync(x => x.Id == id)).LastLoginAtUtc);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cookie_preflight_has_credentials_only_for_exact_enabled_corporate_origin(bool enabled)
    {
        await using var factory = CreateCookieFactory(enabled);
        using var client = CreateCookieClient(factory);
        foreach (var origin in new[] { CorporateOrigin, "https://evil.oroleite.com.br", "http://localhost:5173" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "POST");
            request.Headers.Add("Access-Control-Request-Headers", "content-type,x-orobi-session");
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            var credentials = response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var values) ? values.Single() : null;
            Assert.Equal(enabled && origin == CorporateOrigin ? "true" : null, credentials);
            if (enabled && origin == CorporateOrigin) Assert.Equal(CorporateOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        }
    }

    [Fact]
    public async Task Disabled_cookie_mode_refuses_cookie_login_and_preserves_legacy_bearer()
    {
        await using var factory = CreateCookieFactory(false);
        await SeedAsync(factory);
        using var cookie = CreateCookieClient(factory);
        Assert.Equal(HttpStatusCode.Forbidden, (await CookieLoginAsync(cookie)).StatusCode);
        using var bearer = factory.CreateClient();
        await LoginAsync(bearer);
        Assert.Equal(HttpStatusCode.OK, (await bearer.GetAsync("/api/v1/me")).StatusCode);
        var login = await CookieLoginAsync(bearer);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("accessToken", out _));
        Assert.False(login.Headers.Contains("Set-Cookie"));
        cookie.DefaultRequestHeaders.Add("Cookie", CookieName + "=" + body.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await cookie.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Explicit_invalid_bearer_does_not_fall_back_to_valid_cookie_and_401_does_not_clear_it()
    {
        await using var factory = CreateCookieFactory();
        await SeedAsync(factory);
        using var client = CreateCookieClient(factory);
        SetCookie(client, await CookieLoginAsync(client));
        client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid-synthetic-token");
        var response = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me")).StatusCode);
    }

    [Theory]
    [InlineData("logout")]
    [InlineData("password")]
    [InlineData("reset")]
    [InlineData("inactive-user")]
    [InlineData("inactive-seller")]
    public async Task Cookie_transport_preserves_session_revocation_and_deletes_cookie_only_on_successful_logout_or_own_password_change(string action)
    {
        await using var factory = CreateCookieFactory();
        var id = await SeedAsync(factory);
        using var client = CreateCookieClient(factory);
        SetCookie(client, await CookieLoginAsync(client));
        HttpResponseMessage? changed = null;
        if (action == "logout") changed = await client.PostAsync("/api/v1/auth/logout", null);
        else if (action == "password") changed = await client.PostAsJsonAsync("/api/me/change-password", new { currentPassword = "Synthetic-123!", newPassword = "Changed-456!" });
        else
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OroBiDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == id);
            if (action == "inactive-user") user.IsActive = false;
            if (action == "inactive-seller") (await db.Sellers.SingleAsync()).IsActive = false;
            if (action == "reset")
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                Assert.True((await users.ResetPasswordAsync(user, await users.GeneratePasswordResetTokenAsync(user), "Changed-456!")).Succeeded);
            }
            await db.SaveChangesAsync();
        }
        if (changed is not null)
        {
            Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
            var cleared = Assert.Single(changed.Headers.GetValues("Set-Cookie"));
            Assert.StartsWith(CookieName + "=;", cleared);
            Assert.Contains("httponly", cleared, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secure", cleared, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=strict", cleared, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("domain=", cleared, StringComparison.OrdinalIgnoreCase);
        }
        var denied = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.False(denied.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Cookie_first_access_reuses_backend_gate_and_reauthentication_clears_requirement()
    {
        await using var factory = CreateCookieFactory();
        var id = await SeedAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OroBiDbContext>();
            (await db.Users.SingleAsync(x => x.Id == id)).MustChangePassword = true;
            await db.SaveChangesAsync();
        }
        using var client = CreateCookieClient(factory);
        var login = await CookieLoginAsync(client);
        Assert.True((await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("mustChangePassword").GetBoolean());
        SetCookie(client, login);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me")).StatusCode);
        var denied = await client.GetAsync("/api/v1/me/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal("password_change_required", (await denied.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/me/change-password", new { currentPassword = "Synthetic-123!", newPassword = "Changed-456!" })).StatusCode);
        login = await CookieLoginAsync(client, password: "Changed-456!");
        Assert.False((await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("mustChangePassword").GetBoolean());
        SetCookie(client, login);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me/dashboard")).StatusCode);
    }

    [Fact]
    public async Task Cookie_expiration_has_no_clock_skew_and_server_identity_reports_signed_deadline()
    {
        await using var factory = CreateCookieFactory();
        await SeedAsync(factory);
        using var client = CreateCookieClient(factory);
        var login = await CookieLoginAsync(client);
        var original = new JwtSecurityTokenHandler().ReadJwtToken(login.Headers.GetValues("Set-Cookie").Single().Split(';')[0][(CookieName.Length + 1)..]);
        var expired = new JwtSecurityToken(original.Issuer, original.Audiences.Single(), original.Claims.Where(x => x.Type != "exp" && x.Type != "nbf"), DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddSeconds(-1), new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CookieSigningKey)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Add("Cookie", CookieName + "=" + new JwtSecurityTokenHandler().WriteToken(expired));
        var response = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [InlineData("http://portal-bi.oroleite.com.br")]
    [InlineData("https://*.oroleite.com.br")]
    [InlineData("*")]
    [InlineData("https://portal-bi.oroleite.com.br/path")]
    [InlineData("https://user@portal-bi.oroleite.com.br")]
    [InlineData("https://portal-bi.oroleite.com.br.")]
    [InlineData("https://127.0.0.1")]
    public void Cookie_mode_rejects_unsafe_origin_configuration_at_startup(string origin)
    {
        using var factory = CreateFactory(new()
        {
            ["BrowserSession:Enabled"] = "true",
            ["BrowserSession:AllowedOrigins:0"] = origin
        });
        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() => factory.CreateClient());
    }

    [Fact]
    public async Task Cookie_origin_configuration_replaces_default_allowlist_instead_of_appending_it()
    {
        await using var factory = CreateFactory(new()
        {
            ["BrowserSession:Enabled"] = "true",
            ["BrowserSession:AllowedOrigins:0"] = "https://restricted.oroleite.com.br"
        });
        await SeedAsync(factory);
        using var oldOrigin = CreateCookieClient(factory);
        Assert.Equal(HttpStatusCode.Forbidden, (await CookieLoginAsync(oldOrigin)).StatusCode);
        using var configured = CreateCookieClient(factory, "https://restricted.oroleite.com.br");
        Assert.Equal(HttpStatusCode.OK, (await CookieLoginAsync(configured)).StatusCode);
    }

    [Fact]
    public async Task Explicit_bearer_is_independent_of_ambient_cookie_origin_and_mode_headers()
    {
        await using var factory = CreateCookieFactory();
        await SeedAsync(factory);
        using var bearer = factory.CreateClient();
        await LoginAsync(bearer);
        bearer.DefaultRequestHeaders.Add("Cookie", CookieName + "=invalid-synthetic-cookie");
        bearer.DefaultRequestHeaders.Add("Origin", "https://evil.oroleite.com.br");
        bearer.DefaultRequestHeaders.Add("X-OroBI-Session", "cookie");
        Assert.Equal(HttpStatusCode.OK, (await bearer.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Cookie_mode_is_disabled_by_default_configuration()
    {
        await using var factory = CreateFactory();
        await SeedAsync(factory);
        using var client = CreateCookieClient(factory);
        Assert.Equal(HttpStatusCode.Forbidden, (await CookieLoginAsync(client)).StatusCode);
    }
    [Theory]
    [InlineData("*.oroleite.com.br")]
    [InlineData("api-bi.oroleite.com.br.")]
    [InlineData("127.0.0.1")]
    public void Cookie_mode_rejects_unsafe_api_host_configuration_at_startup(string host)
    {
        using var factory = CreateFactory(new()
        {
            ["BrowserSession:Enabled"] = "true",
            ["BrowserSession:ApiHost"] = host
        });
        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() => factory.CreateClient());
    }
    private static WebApplicationFactory<Program> CreateCookieFactory(bool enabled = true) => CreateFactory(new()
    {
        ["BrowserSession:Enabled"] = enabled.ToString(),
        ["BrowserSession:ApiHost"] = "api-bi.oroleite.com.br",
        ["BrowserSession:AllowedOrigins:0"] = CorporateOrigin,
        ["Jwt:SigningKey"] = CookieSigningKey,
        ["Cors:Origins:0"] = "http://localhost:5173"
    });

    private static HttpClient CreateCookieClient(WebApplicationFactory<Program> factory, string? origin = CorporateOrigin, string? header = "cookie", string address = CorporateApi)
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri(address), HandleCookies = false, AllowAutoRedirect = false });
        if (origin is not null) client.DefaultRequestHeaders.Add("Origin", origin);
        if (header is not null) client.DefaultRequestHeaders.Add("X-OroBI-Session", header);
        return client;
    }

    private static Task<HttpResponseMessage> CookieLoginAsync(HttpClient client, string prefix = "/api/v1", string password = "Synthetic-123!") =>
        client.PostAsJsonAsync(prefix + "/auth/login", new { email = "seller@example.invalid", password });

    private static void SetCookie(HttpClient client, HttpResponseMessage response)
    {
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", response.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
    }
}
