using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OroBI.Application.Identity;

namespace OroBI.Api.IntegrationTests.Auth;

public sealed class LoginTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _application;

    public LoginTests(WebApplicationFactory<Program> factory)
    {
        _application = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = "orobi-tests",
                    ["Jwt:Audience"] = "orobi-tests",
                    ["Jwt:SigningKey"] = "test-signing-key-with-at-least-thirty-two-characters"
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ILocalAuthenticationService>();
                services.AddScoped<ILocalAuthenticationService, TestLocalAuthenticationService>();
            });
        });
    }

    [Fact]
    public async Task Valid_local_login_returns_access_token()
    {
        using var client = _application.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@orobi.local",
            password = "Test123!"
        });

        var content = await response.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        Assert.False(string.IsNullOrWhiteSpace(content.AccessToken));
    }

    [Fact]
    public async Task Versioned_local_login_returns_access_token()
    {
        using var client = _application.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "admin@orobi.local", password = "Test123!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Current_user_requires_authentication()
    {
        using var client = _application.CreateClient();

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record LoginResponse(string AccessToken);
}

internal sealed class TestLocalAuthenticationService : ILocalAuthenticationService
{
    public Task<LocalLoginResult?> LoginAsync(string email, string password, CancellationToken cancellationToken) =>
        Task.FromResult<LocalLoginResult?>(email == "admin@orobi.local" && password == "Test123!"
            ? new LocalLoginResult("test-access-token", DateTime.UtcNow.AddHours(1), ["Administrador"])
            : null);
}
