using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OroBI.Application.Closings;

namespace OroBI.Api.IntegrationTests.Closings;

public sealed class ClosingEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ClosingEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
                services.RemoveAll<ISellerClosingQueryService>();
                services.AddScoped<ISellerClosingQueryService, TestSellerClosingQueryService>();
            });
        }).CreateClient();
    }

    [Fact]
    public async Task Get_closing_returns_brand_awards_for_standard_seller()
    {
        var response = await _client.GetAsync("/api/closings?seller=ANA&month=2026-08");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("brandAwards", body, StringComparison.Ordinal);
        Assert.Contains("NESTLE", body, StringComparison.Ordinal);
    }
}

internal sealed class TestSellerClosingQueryService : ISellerClosingQueryService
{
    public Task<SellerClosingSummary?> GetAsync(string seller, int year, int month, CancellationToken cancellationToken) =>
        Task.FromResult<SellerClosingSummary?>(new SellerClosingSummary(
            new PppSummary(75m, 300m),
            100m,
            50m,
            25m,
            new CompensationSummary(120m, 2120m),
            475m)
        {
            BrandAwards = [new ClosingBrandAward("NESTLE", 50m, 100m, 25m)]
        });
}
