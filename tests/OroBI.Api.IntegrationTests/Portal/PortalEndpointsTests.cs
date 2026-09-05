using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OroBI.Application.Identity;
using OroBI.Domain.Closings;
using OroBI.Domain.Commercial;
using OroBI.Domain.Imports;
using OroBI.Domain.Sellers;
using OroBI.Infrastructure.Identity;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Api.IntegrationTests.Portal;

public sealed class PortalEndpointsTests
{
    [Theory]
    [InlineData("dashboard")]
    [InlineData("sales")]
    [InlineData("customers")]
    [InlineData("products")]
    [InlineData("brands")]
    [InlineData("goals")]
    [InlineData("ppp")]
    [InlineData("trades")]
    [InlineData("commission")]
    [InlineData("closings")]
    public async Task Seller_cannot_choose_another_seller_in_personal_or_management_routes(string resource)
    {
        await using var fixture = await PortalFixture.CreateAsync();
        var response = await fixture.Client.GetAsync($"/api/v1/me/{resource}?month=2026-08&sellerId={fixture.OtherSeller}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        response = await fixture.Client.GetAsync($"/api/v1/management/sellers/{fixture.OtherSeller}/{resource}?month=2026-08");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("dashboard")]
    [InlineData("sales")]
    [InlineData("customers")]
    [InlineData("products")]
    [InlineData("brands")]
    [InlineData("trades")]
    public async Task Personal_results_only_contain_own_data_without_cost(string resource)
    {
        await using var fixture = await PortalFixture.CreateAsync();
        var response = await fixture.Client.GetAsync($"/api/v1/me/{resource}?startDate=2026-08-01&endDate=2026-08-31");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PRIVATE-OTHER", json);
        Assert.DoesNotContain("876543", json);
        Assert.DoesNotContain("unitCost", json);
        Assert.DoesNotContain("margin", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Manager_can_access_assigned_seller_only()
    {
        await using var fixture = await PortalFixture.CreateAsync("Gestor");
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync($"/api/v1/management/sellers/{fixture.OwnSeller}/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.GetAsync($"/api/v1/management/sellers/{fixture.OtherSeller}/dashboard")).StatusCode);
        var list = await fixture.Client.GetStringAsync("/api/v1/management/sellers");
        Assert.Contains(fixture.OwnSeller.ToString(), list);
        Assert.DoesNotContain(fixture.OtherSeller.ToString(), list);
    }

    [Fact]
    public async Task Hidden_financial_permissions_apply_to_closing_and_dashboard()
    {
        await using var fixture = await PortalFixture.CreateAsync();
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OroBiDbContext>();
            var access = await db.UserSellerAccesses.SingleAsync(item => item.SellerId == fixture.OwnSeller);
            access.Permissions = access.Permissions with { CanViewRevenue = false, CanViewPrize = false };
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.GetAsync("/api/v1/me/dashboard")).StatusCode);
        var response = await fixture.Client.GetAsync("/api/v1/me/commission?month=2026-08");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(JsonValueKind.Null, json.GetProperty("revenue").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("totalAwards").ValueKind);
        Assert.Equal(10m, json.GetProperty("commission").GetDecimal());
    }

    [Fact]
    public async Task Customer_permission_is_applied_to_aggregate_responses()
    {
        await using var fixture = await PortalFixture.CreateAsync();
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OroBiDbContext>();
            var access = await db.UserSellerAccesses.SingleAsync(item => item.SellerId == fixture.OwnSeller);
            access.Permissions = access.Permissions with { CanViewCustomers = false };
            db.Add(OroBI.Domain.Ppp.PppRecord.Create(Guid.NewGuid(), 2026, 8, "ANA", "SEGMENTO", 10, 4, 30));
            await db.SaveChangesAsync();
        }
        foreach (var resource in new[] { "products", "brands" })
        {
            var json = JsonDocument.Parse(await fixture.Client.GetStringAsync($"/api/v1/me/{resource}?month=2026-08")).RootElement;
            Assert.Equal(JsonValueKind.Null, json.GetProperty("items")[0].GetProperty("customerCount").ValueKind);
        }
        var ppp = JsonDocument.Parse(await fixture.Client.GetStringAsync("/api/v1/me/ppp?month=2026-08")).RootElement;
        Assert.Equal(JsonValueKind.Null, ppp.GetProperty("segments")[0].GetProperty("customerCount").ValueKind);
        var dashboard = JsonDocument.Parse(await fixture.Client.GetStringAsync("/api/v1/me/dashboard?month=2026-08")).RootElement;
        Assert.Equal(JsonValueKind.Null, dashboard.GetProperty("period").GetProperty("customerCount").ValueKind);
    }

    [Fact]
    public async Task Invalid_dates_paging_and_foreign_customer_are_rejected()
    {
        await using var fixture = await PortalFixture.CreateAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.GetAsync("/api/v1/me/sales?startDate=2026-09-02&endDate=2026-08-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.GetAsync("/api/v1/me/sales?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.GetAsync("/api/v1/me/closings?month=oops")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.GetAsync("/api/v1/me/closings?month=9999-12")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync("/api/v1/me/customers/OTHER?startDate=2026-08-01&endDate=2026-08-31")).StatusCode);
    }

    [Fact]
    public async Task Administrator_approves_same_official_snapshot_seen_by_seller()
    {
        await using var fixture = await PortalFixture.CreateAsync();
        var sellerToken = fixture.Client.DefaultRequestHeaders.Authorization;
        await fixture.LoginAsAdminAsync();
        var route = $"/api/v1/management/sellers/{fixture.OwnSeller}/closings";
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PostAsync(route + "/review?month=2026-08", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PostAsync(route + "/approve?month=2026-08", null)).StatusCode);
        var approved = await fixture.Client.GetStringAsync(route + "?month=2026-08");
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OroBiDbContext>();
            db.Add(CommercialMovement.Create(Guid.NewGuid(), new(2026, 8, 2), "ANA", "VENDA", 5000m, 1m));
            await db.SaveChangesAsync();
        }
        var legacy = JsonDocument.Parse(await fixture.Client.GetStringAsync("/api/v1/closings?seller=ANA&month=2026-08")).RootElement;
        Assert.Equal(10m, legacy.GetProperty("compensation").GetProperty("commission").GetDecimal());
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.PostAsync(route + "/approve?month=2026-08", null)).StatusCode);
        fixture.Client.DefaultRequestHeaders.Authorization = sellerToken;
        Assert.Equal(approved, await fixture.Client.GetStringAsync("/api/v1/me/closings?month=2026-08"));
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.PostAsync(route + "/approve?month=2026-08", null)).StatusCode);
    }
}

internal sealed class PortalFixture : IAsyncDisposable
{
    public WebApplicationFactory<Program> Factory { get; }
    public HttpClient Client { get; }
    public Guid OwnSeller { get; private set; }
    public Guid OtherSeller { get; private set; }
    private PortalFixture(WebApplicationFactory<Program> factory) { Factory = factory; Client = factory.CreateClient(); }

    public static async Task<PortalFixture> CreateAsync(string role = "Vendedor")
    {
        var database = Guid.NewGuid().ToString();
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<OroBiDbContext>();
                services.RemoveAll<DbContextOptions<OroBiDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<OroBiDbContext>>();
                services.AddDbContext<OroBiDbContext>(options => options.UseInMemoryDatabase(database));
            });
        });
        var fixture = new PortalFixture(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OroBiDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var name in new[] { role, "Administrador" }.Distinct())
                Assert.True((await roles.CreateAsync(new IdentityRole(name))).Succeeded);
            var user = new ApplicationUser { Email = "ana@example.invalid", UserName = "ana@example.invalid" };
            Assert.True((await users.CreateAsync(user, "Synthetic-123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
            var admin = new ApplicationUser { Email = "admin@example.invalid", UserName = "admin@example.invalid" };
            Assert.True((await users.CreateAsync(admin, "Synthetic-123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(admin, "Administrador")).Succeeded);
            var own = new Seller { Name = "Ana", ImportedName = "ANA" };
            var other = new Seller { Name = "PRIVATE-OTHER", ImportedName = "BIA" };
            fixture.OwnSeller = own.Id; fixture.OtherSeller = other.Id;
            db.AddRange(own, other, new UserSellerAccess { UserId = user.Id, SellerId = own.Id });
            var values = ImportBatch.Start(ImportFileType.GoalValues, "values.csv", "values");
            values.Complete("memory://values", 1, 0);
            var power = ImportBatch.Start(ImportFileType.Power, "power.csv", "power");
            power.Complete("memory://power", 2, 0);
            db.AddRange(values, power, ImportedClosingDefaults.Create(values.Id, 2000m, 1m, 1200m, new Dictionary<string, decimal>()),
                CommercialMovement.CreateFromImport(power.Id, new(2026, 8, 1), "ANA", "OROLEITE", "G", "VENDA", "CIDADE", "CLIENTE ANA", "LEITE", 1000m, 10m, 5m, "OWN", "NF1"),
                CommercialMovement.CreateFromImport(power.Id, new(2026, 8, 1), "BIA", "PRIVATE-OTHER", "G", "VENDA", "CIDADE", "PRIVATE-OTHER", "PRIVATE-OTHER", 876543m, 10m, 9m, "OTHER", "NF2"));
            await db.SaveChangesAsync();
        }
        await fixture.LoginAsync("ana@example.invalid");
        return fixture;
    }

    public Task LoginAsAdminAsync() => LoginAsync("admin@example.invalid");
    private async Task LoginAsync(string email)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Synthetic-123!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<LocalLoginResult>();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
    }
    public async ValueTask DisposeAsync() { Client.Dispose(); await Factory.DisposeAsync(); }
}
