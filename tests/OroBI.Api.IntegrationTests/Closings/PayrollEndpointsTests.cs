using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OroBI.Application.Closings;

namespace OroBI.Api.IntegrationTests.Closings;

public sealed class PayrollEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public PayrollEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "PayrollTest";
                    options.DefaultChallengeScheme = "PayrollTest";
                    options.DefaultForbidScheme = "PayrollTest";
                }).AddScheme<AuthenticationSchemeOptions, PayrollAuthenticationHandler>("PayrollTest", _ => { });
                services.RemoveAll<IPayrollClosingQueryService>();
                services.AddScoped<IPayrollClosingQueryService, PayrollQueryFixture>();
            });
        }).CreateClient();
    }

    [Theory]
    [InlineData("/api/closings/payroll", "Vendedor", HttpStatusCode.Forbidden)]
    [InlineData("/api/closings/payroll/export", "Vendedor", HttpStatusCode.Forbidden)]
    [InlineData("/api/closings/payroll", null, HttpStatusCode.Unauthorized)]
    [InlineData("/api/closings/payroll/export", null, HttpStatusCode.Unauthorized)]
    [InlineData("/api/closings/payroll", "Gestor", HttpStatusCode.OK)]
    [InlineData("/api/closings/payroll/export", "Gestor", HttpStatusCode.OK)]
    [InlineData("/api/closings/payroll", "Administrador", HttpStatusCode.OK)]
    [InlineData("/api/closings/payroll/export", "Administrador", HttpStatusCode.OK)]
    public async Task Payroll_requires_a_manager_or_administrator(string route, string? role, HttpStatusCode expected)
    {
        using var response = await Send(route + "?month=2026-08", role);
        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?month=2026-8")]
    [InlineData("?month=2026-13")]
    [InlineData("?month=0000-08")]
    [InlineData("?month=2026-08-01")]
    [InlineData("?month=2026-08&coverageSeller=DEIVID")]
    [InlineData("?month=2026-08&coverageSeller=%20")]
    [InlineData("?month=2026-08&coverageSeller=")]
    public async Task Payroll_and_export_reject_invalid_parameters(string query)
    {
        foreach (var route in new[] { "/api/closings/payroll", "/api/closings/payroll/export" })
        {
            using var response = await Send(route + query);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("/api/closings/payroll")]
    [InlineData("/api/closings/payroll/export")]
    public async Task Payroll_returns_clear_not_found_when_required_configuration_is_missing(string route)
    {
        using var response = await Send(route + "?month=2026-07");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("error").GetString()));
        Assert.Contains("configura", body.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/api/closings/payroll")]
    [InlineData("/api/v1/closings/payroll")]
    public async Task Payroll_returns_period_default_coverage_and_full_precision_totals(string route)
    {
        using var response = await Send(route + "?month=2026-08");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal(2026, body.GetProperty("year").GetInt32());
        Assert.Equal(8, body.GetProperty("month").GetInt32());
        Assert.Equal("MARCIO LUIZ DA ROSA", body.GetProperty("coverageSeller").GetString());
        Assert.Equal(125.125m, body.GetProperty("totalCommission").GetDecimal());
        Assert.Equal(170m, body.GetProperty("totalIncentives").GetDecimal());
        Assert.Equal(2246.125m, body.GetProperty("total").GetDecimal());
        Assert.Equal("TIAGO", body.GetProperty("rows")[0].GetProperty("seller").GetString());
    }

    [Fact]
    public async Task Payroll_canonicalizes_coverage_and_export_matches_the_selected_response()
    {
        const string query = "?month=2026-09&coverageSeller=%20rodrigo%20";
        using var response = await Send("/api/closings/payroll" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal("RODRIGO KEHL", body.GetProperty("coverageSeller").GetString());
        Assert.Equal(9, body.GetProperty("month").GetInt32());

        using var export = await Send("/api/closings/payroll/export" + query);
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", export.Content.Headers.ContentType?.MediaType);
        Assert.Equal("fechamento-rh-2026-09.xlsx", export.Content.Headers.ContentDisposition?.FileNameStar);
        using var archive = new ZipArchive(new MemoryStream(await export.Content.ReadAsByteArrayAsync()));
        using var worksheetStream = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var worksheet = XDocument.Load(worksheetStream);
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Assert.Contains(worksheet.Descendants(spreadsheet + "t"), value => value.Value.Contains("RODRIGO KEHL", StringComparison.Ordinal));
        Assert.Contains(worksheet.Descendants(spreadsheet + "t"), value => value.Value.Contains("2026-09", StringComparison.Ordinal));
        var totalRow = worksheet.Descendants(spreadsheet + "row").Single(row => row.Descendants(spreadsheet + "t").Any(value => value.Value == "TOTAL"));
        var totalCell = totalRow.Elements(spreadsheet + "c").Last();
        Assert.Equal(body.GetProperty("total").GetDecimal(), decimal.Parse(totalCell.Element(spreadsheet + "v")!.Value, CultureInfo.InvariantCulture));
    }

    private Task<HttpResponseMessage> Send(string path, string? role = "Gestor")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (role is not null) request.Headers.Add("X-Payroll-Test-Role", role);
        return _client.SendAsync(request);
    }
}

internal sealed class PayrollQueryFixture : IPayrollClosingQueryService
{
    public Task<PayrollClosing?> GetPayrollAsync(string coverageSeller, int year, int month, CancellationToken cancellationToken) =>
        Task.FromResult<PayrollClosing?>(month == 7 ? null : new PayrollClosing(year, month, coverageSeller, PayrollCatalog.CoverageSellers,
            [new PayrollClosingRow("TIAGO", coverageSeller, "Cobertura: " + coverageSeller, 12512.50m, 1951m, 1m, 125.125m, 100m, 50m, 20m)]));
}

internal sealed class PayrollAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Payroll-Test-Role"].ToString();
        if (string.IsNullOrEmpty(role)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "payroll-test"), new Claim(ClaimTypes.Role, role)], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
