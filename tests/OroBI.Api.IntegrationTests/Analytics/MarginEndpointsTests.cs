using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OroBI.Application.Analytics;
using OroBI.Domain.Commercial;

namespace OroBI.Api.IntegrationTests.Analytics;

public sealed class MarginEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly MarginQueryFixture _service = new();

    public MarginEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "MarginTest";
                    options.DefaultChallengeScheme = "MarginTest";
                    options.DefaultForbidScheme = "MarginTest";
                }).AddScheme<AuthenticationSchemeOptions, MarginAuthenticationHandler>("MarginTest", _ => { });
                services.RemoveAll<ICommercialAnalyticsQueryService>();
                services.AddSingleton<ICommercialAnalyticsQueryService>(_service);
            });
        }).CreateClient();
    }

    [Theory]
    [InlineData("/api/margins", 4)]
    [InlineData("/api/v1/margins", 4)]
    [InlineData("/api/net-margin", 9)]
    [InlineData("/api/v1/net-margin", 9)]
    public async Task Summary_preserves_the_numeric_only_contract_consumed_by_the_live_interface(string route, int fieldCount)
    {
        using var response = await Send(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal(fieldCount, body.EnumerateObject().Count());
        Assert.False(body.TryGetProperty("groups", out _));
        Assert.All(body.EnumerateObject(), field => Assert.Equal(JsonValueKind.Number, field.Value.ValueKind));
        if (route.EndsWith("/margins", StringComparison.Ordinal))
        {
            Assert.Equal(100m, body.GetProperty("revenue").GetDecimal());
            Assert.Equal(40m, body.GetProperty("cost").GetDecimal());
            Assert.Equal(60m, body.GetProperty("grossProfit").GetDecimal());
            Assert.Equal(60m, body.GetProperty("marginPercent").GetDecimal());
        }
        else
        {
            Assert.Equal(100m, body.GetProperty("grossSales").GetDecimal());
            Assert.Equal(20m, body.GetProperty("returns").GetDecimal());
            Assert.Equal(80m, body.GetProperty("netSales").GetDecimal());
            Assert.Equal(32m, body.GetProperty("netCost").GetDecimal());
            Assert.Equal(0m, body.GetProperty("tradeLosses").GetDecimal());
            Assert.Equal(0m, body.GetProperty("boletoDiscounts").GetDecimal());
            Assert.Equal(48m, body.GetProperty("liquidProfit").GetDecimal());
            Assert.Equal(60m, body.GetProperty("liquidMarginPercent").GetDecimal());
            Assert.Equal(1, body.GetProperty("productCount").GetInt32());
        }
    }

    [Theory]
    [InlineData("/api/margins/details", false)]
    [InlineData("/api/v1/margins/details", false)]
    [InlineData("/api/net-margin/details", true)]
    [InlineData("/api/v1/net-margin/details", true)]
    public async Task Details_expose_numeric_totals_counts_and_grouped_financial_rows(string route, bool net)
    {
        using var response = await Send(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        var groups = body.GetProperty("groups");
        Assert.Equal(net ? 6 : 3, groups.EnumerateObject().Count());
        Assert.Equal(net ? 2 : 1, body.GetProperty("movementCount").GetInt32());
        var product = Assert.Single(groups.GetProperty("product").EnumerateArray());
        Assert.Equal("LEITE", product.GetProperty("label").GetString());
        Assert.Equal(net ? 48m : 60m, product.GetProperty(net ? "liquidProfit" : "grossProfit").GetDecimal());
        Assert.Equal(net ? 3m : 2m, product.GetProperty("quantity").GetDecimal());
        Assert.Equal(60m, product.GetProperty(net ? "liquidMarginPercent" : "marginPercent").GetDecimal());
        if (net)
        {
            Assert.Equal(20m, product.GetProperty("ownReturns").GetDecimal());
            Assert.Equal(0m, product.GetProperty("customerReturns").GetDecimal());
            Assert.Equal(32m, product.GetProperty("netCost").GetDecimal());
        }
    }

    [Theory]
    [InlineData("/api/margins")]
    [InlineData("/api/net-margin")]
    [InlineData("/api/margins/details")]
    [InlineData("/api/net-margin/details")]
    public async Task Margin_routes_require_manager_or_administrator(string route)
    {
        using var forbidden = await Send(route, "Vendedor");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var manager = await Send(route, "Gestor");
        Assert.Equal(HttpStatusCode.Forbidden, manager.StatusCode);
        using var unauthorized = await Send(route, null);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        using var administrator = await Send(route, "Administrador");
        Assert.Equal(HttpStatusCode.OK, administrator.StatusCode);
    }

    [Theory]
    [InlineData("/api/margins")]
    [InlineData("/api/net-margin")]
    [InlineData("/api/margins/details")]
    [InlineData("/api/net-margin/details")]
    public async Task Margin_routes_forward_all_commercial_query_filters(string route)
    {
        using var response = await Send(route + "?startDate=2026-08-01&endDate=2026-08-31&seller=Ana&brand=Oroleite&group=Laticinios&city=Goiania&customerContains=Mercado&productContains=Leite&movementTypes=VENDA&movementTypes=DEVOLUCAO");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var filter = Assert.IsType<CommercialFilter>(_service.LastFilter);
        Assert.Equal(new DateOnly(2026, 8, 1), filter.StartDate);
        Assert.Equal(new DateOnly(2026, 8, 31), filter.EndDate);
        Assert.Equal("Ana", filter.Seller);
        Assert.Equal("Oroleite", filter.Brand);
        Assert.Equal("Laticinios", filter.Group);
        Assert.Equal("Goiania", filter.City);
        Assert.Equal("Mercado", filter.CustomerContains);
        Assert.Equal("Leite", filter.ProductContains);
        Assert.Equal(["VENDA", "DEVOLUCAO"], filter.MovementTypes);
    }

    private Task<HttpResponseMessage> Send(string route, string? role = "Administrador")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, route);
        if (role is not null) request.Headers.Add("X-Margin-Test-Role", role);
        return _client.SendAsync(request);
    }
}

internal sealed class MarginQueryFixture : ICommercialAnalyticsQueryService
{
    public CommercialFilter? LastFilter { get; private set; }

    private static readonly CommercialMovement[] Movements = [
        CommercialMovement.CreateFromImport(Guid.NewGuid(), new DateOnly(2026, 8, 1), "ANA", "OROLEITE", "LATICINIOS", "VENDA", "GOIANIA", "MERCADO", "LEITE", 100m, 2m, 20m, "1", "1"),
        CommercialMovement.CreateFromImport(Guid.NewGuid(), new DateOnly(2026, 8, 1), "ANA", "OROLEITE", "LATICINIOS", "DEVOLUCAO", "GOIANIA", "MERCADO", "LEITE", -20m, -1m, 8m, "1", "2")];

    public Task<MarginSummary> GetMarginsAsync(CommercialFilter filter, CancellationToken cancellationToken)
    {
        LastFilter = filter;
        return Task.FromResult(MarginCalculator.Calculate(Movements));
    }

    public Task<NetMarginReport> GetNetMarginAsync(CommercialFilter filter, CancellationToken cancellationToken)
    {
        LastFilter = filter;
        return Task.FromResult(NetMarginCalculator.Calculate(Movements));
    }

    public Task<TradeSummary> GetTradesAsync(CommercialFilter filter, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<TradeAnalysisReport> GetTradeAnalysisAsync(CommercialFilter filter, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<SalesTradeSummary> GetSalesTradesAsync(CommercialFilter filter, CancellationToken cancellationToken) => throw new NotSupportedException();
}

internal sealed class MarginAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Margin-Test-Role"].ToString();
        if (string.IsNullOrEmpty(role)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "margin-test"), new Claim(ClaimTypes.Role, role)], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
