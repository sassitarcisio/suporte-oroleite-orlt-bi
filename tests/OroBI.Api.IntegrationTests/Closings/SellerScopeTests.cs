using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OroBI.Application.Closings;

namespace OroBI.Api.IntegrationTests.Closings;

public sealed class SellerScopeTests
{
    public static IEnumerable<object?[]> AccessCases()
    {
        foreach (var prefix in new[] { "/api", "/api/v1" })
        {
            yield return [prefix, "Vendedor", "ANA", "ANA", HttpStatusCode.OK];
            yield return [prefix, "Vendedor", "ANA", "OUTRO", HttpStatusCode.Forbidden];
            yield return [prefix, "Vendedor", null, "ANA", HttpStatusCode.Forbidden];
            yield return [prefix, "Vendedor", " ", "ANA", HttpStatusCode.Forbidden];
            yield return [prefix, "Vendedor", "RODRIGO KEHL", "VENDEDOR: RODRIGO", HttpStatusCode.OK];
            yield return [prefix, "Vendedor", "VENDEDOR: MARCELO IVONEI DA ROSA", "marcelo da rosa", HttpStatusCode.OK];
            yield return [prefix, "Gestor", null, "OUTRO", HttpStatusCode.OK];
            yield return [prefix, "Administrador", null, "OUTRO", HttpStatusCode.OK];
            yield return [prefix, "OutroPerfil", "ANA", "ANA", HttpStatusCode.Forbidden];
            yield return [prefix, null, null, "ANA", HttpStatusCode.Unauthorized];
        }
    }

    [Theory]
    [MemberData(nameof(AccessCases))]
    public async Task Closing_enforces_seller_identity_and_privileged_roles(
        string prefix, string? role, string? claim, string seller, HttpStatusCode expected)
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "SellerTest";
                    options.DefaultChallengeScheme = "SellerTest";
                    options.DefaultForbidScheme = "SellerTest";
                }).AddScheme<AuthenticationSchemeOptions, SellerAuthenticationHandler>("SellerTest", _ => { });
                services.RemoveAll<ISellerClosingQueryService>();
                services.AddScoped<ISellerClosingQueryService, TestSellerClosingQueryService>();
            });
        });
        using var client = factory.CreateClient();
        if (role is not null) client.DefaultRequestHeaders.Add("Test-Role", role);
        if (claim is not null) client.DefaultRequestHeaders.TryAddWithoutValidation("Test-Seller", claim);

        var response = await client.GetAsync($"{prefix}/closings?seller={Uri.EscapeDataString(seller)}&month=2026-08");

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
            Assert.Contains("compensation", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}

internal sealed class SellerAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Test-Role", out var role))
            return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "synthetic-user"), new(ClaimTypes.Role, role.ToString()) };
        if (Request.Headers.TryGetValue("Test-Seller", out var seller)) claims.Add(new("seller", seller.ToString()));
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
