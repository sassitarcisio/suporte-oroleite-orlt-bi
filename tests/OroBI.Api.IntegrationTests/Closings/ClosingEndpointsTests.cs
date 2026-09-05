using System.Net;
using System.Text.Json;
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
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal(2000m, json.GetProperty("compensation").GetProperty("baseSalary").GetDecimal());
        Assert.Equal(2595m, json.GetProperty("total").GetDecimal());
        Assert.Equal(12000m, json.GetProperty("monthly").GetProperty("revenue").GetDecimal());
        Assert.Equal("2026-08-01", json.GetProperty("monthly").GetProperty("documents")[0].GetProperty("date").GetString());
        Assert.Equal(75m, json.GetProperty("pppSegments")[0].GetProperty("achievementPercent").GetDecimal());
        Assert.Equal(1000m, json.GetProperty("brandAwards")[0].GetProperty("revenueGoal").GetDecimal());
    }

    [Fact]
    public async Task Get_closing_describes_the_missing_configuration_when_no_result_is_available()
    {
        var response = await _client.GetAsync("/api/closings?seller=SEM_CONFIG&month=2026-08");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.NotFound, body);
        Assert.Contains("VALOR_METAS", body, StringComparison.Ordinal);
    }
}

internal sealed class TestSellerClosingQueryService : ISellerClosingQueryService
{
    public Task<SellerClosingSummary?> GetAsync(string seller, int year, int month, CancellationToken cancellationToken) =>
        Task.FromResult<SellerClosingSummary?>(seller == "SEM_CONFIG" ? null : new SellerClosingSummary(
            new PppSummary(75m, 300m),
            100m,
            50m,
            25m,
            new CompensationSummary(120m, 2120m),
            475m)
        {
            BrandAwards = [new ClosingBrandAward("NESTLE", 50m, 100m, 25m) { RevenueGoal = 1000m }],
            Monthly = new ClosingMonthlySummary("seller", 12000m, 12000m, 0m, 0m, 1, 1,
                [new ClosingDocument("NF1", new DateOnly(2026, 8, 1), "ANA", "1", "CLIENTE", "VENDA", 12000m)]),
            PppSegments = [new ClosingPppSegment("MERCADO", 10, 4, 30)]
        });

    public Task<ClosingConfigurationStatus> GetConfigurationStatusAsync(string seller, int year, int month, CancellationToken cancellationToken) =>
        Task.FromResult(new ClosingConfigurationStatus(false, false, false, false));
}
