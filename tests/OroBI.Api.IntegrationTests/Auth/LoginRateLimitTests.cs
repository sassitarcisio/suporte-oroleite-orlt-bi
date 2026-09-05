using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OroBI.Application.Identity;

namespace OroBI.Api.IntegrationTests.Auth;

public sealed class LoginRateLimitTests
{
    [Fact]
    public async Task Account_limit_is_shared_by_route_versions_and_normalized_email()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.invalid", password = "invalid" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var limited = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = " MISSING@EXAMPLE.INVALID ", password = "invalid" });

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        var otherAccount = await client.PostAsJsonAsync("/api/auth/login", new { email = "other@example.invalid", password = "invalid" });
        Assert.Equal(HttpStatusCode.Unauthorized, otherAccount.StatusCode);
    }

    [Fact]
    public async Task Distinct_accounts_behind_same_proxy_do_not_share_login_allowance()
    {
        await using var factory = CreateFactory();
        for (var attempt = 0; attempt < 31; attempt++)
        {
            var response = await SendFromAddressAsync(factory, $"missing{attempt}@example.invalid", "192.0.2.1", "198.51.100.1");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Same_account_shares_limit_across_connection_addresses_and_forwarded_headers()
    {
        await using var factory = CreateFactory();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var response = await SendFromAddressAsync(factory, "missing@example.invalid", $"192.0.2.{attempt + 1}", $"198.51.100.{attempt + 1}");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var limited = await SendFromAddressAsync(factory, " MISSING@EXAMPLE.INVALID ", "203.0.113.1", "203.0.113.2");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendFromAddressAsync(WebApplicationFactory<Program> factory, string email, string address, string forwardedFor)
    {
        using var client = new HttpClient(factory.Server.CreateHandler(context => context.Connection.RemoteIpAddress = IPAddress.Parse(address)))
        {
            BaseAddress = new Uri("https://localhost")
        };
        client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        return await client.PostAsJsonAsync("/api/auth/login", new { email, password = "invalid" });
    }

    private static WebApplicationFactory<Program> CreateFactory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ILocalAuthenticationService>();
            services.AddScoped<ILocalAuthenticationService, TestLocalAuthenticationService>();
        });
    });
}
