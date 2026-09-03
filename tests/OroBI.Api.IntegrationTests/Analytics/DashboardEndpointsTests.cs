using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OroBI.Application.Analytics;

namespace OroBI.Api.IntegrationTests.Analytics;

public sealed class DashboardEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public DashboardEndpointsTests(WebApplicationFactory<Program> factory)
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
                services.RemoveAll<IDashboardQueryService>();
                services.AddScoped<IDashboardQueryService, TestDashboardQueryService>();
                services.RemoveAll<ICommercialFilterOptionsQueryService>();
                services.AddScoped<ICommercialFilterOptionsQueryService, TestCommercialFilterOptionsQueryService>();
            });
        }).CreateClient();
    }

    [Fact]
    public async Task Get_dashboard_returns_ok()
    {
        var response = await _client.GetAsync("/api/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_versioned_dashboard_returns_ok()
    {
        var response = await _client.GetAsync("/api/v1/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_dashboard_filter_options_returns_ok()
    {
        var response = await _client.GetAsync("/api/dashboard/filter-options");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_dashboard_details_returns_ok()
    {
        var response = await _client.GetAsync("/api/dashboard/details");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_dashboard_maps_commercial_filters()
    {
        var response = await _client.GetAsync(
            "/api/dashboard?startDate=2026-01-01&endDate=2026-01-31&seller=Ana&brand=Oroleite&group=Laticinios&city=Goiania&customerContains=Mercado&productContains=Leite&movementTypes=VENDA&movementTypes=TROCA");

        var summary = await response.Content.ReadFromJsonAsync<DashboardSummary>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(summary);
        Assert.Equal(123m, summary.GrossSales);
    }
}

internal sealed class TestDashboardQueryService : IDashboardQueryService
{
    public Task<DashboardSummary> GetAsync(CommercialFilter filter, CancellationToken cancellationToken)
    {
        var isExpectedFilter = filter.StartDate == new DateOnly(2026, 1, 1)
            && filter.EndDate == new DateOnly(2026, 1, 31)
            && filter.Seller == "Ana"
            && filter.Brand == "Oroleite"
            && filter.Group == "Laticinios"
            && filter.City == "Goiania"
            && filter.CustomerContains == "Mercado"
            && filter.ProductContains == "Leite"
            && filter.MovementTypes is not null
            && filter.MovementTypes.SequenceEqual(["VENDA", "TROCA"]);

        return Task.FromResult(new DashboardSummary(isExpectedFilter ? 123m : 0m, 0m, 0m, 0m, 0m, 0, 0, 0));
    }

    public Task<DashboardDetails> GetDetailsAsync(CommercialFilter filter, CancellationToken cancellationToken) =>
        Task.FromResult(new DashboardDetails([], []));
}

internal sealed class TestCommercialFilterOptionsQueryService : ICommercialFilterOptionsQueryService
{
    public Task<CommercialFilterOptions> GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new CommercialFilterOptions(["OROLEITE"], ["LATICINIOS"], ["GOIANIA"], ["VENDA"]));
}
