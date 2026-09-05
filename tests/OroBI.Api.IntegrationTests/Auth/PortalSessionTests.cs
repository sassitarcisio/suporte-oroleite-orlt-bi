using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OroBI.Application.Identity;
using OroBI.Domain.Sellers;
using OroBI.Infrastructure.Identity;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Api.IntegrationTests.Auth;

public sealed class PortalSessionTests
{
    [Fact]
    public async Task Removing_access_retains_inactive_link_history_and_denies_scope()
    {
        await using var factory = CreateFactory();
        var userId = await SeedAsync(factory, "Administrador");
        using var client = factory.CreateClient();
        await LoginAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/v1/admin/users/{userId}/access", new { role = "Gestor", sellerAccesses = Array.Empty<object>() })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OroBiDbContext>();
        var retained = Assert.Single(await db.UserSellerAccesses.Where(a => a.UserId == userId).ToArrayAsync());
        Assert.False(retained.IsActive);
        Assert.True(retained.Permissions.CanViewRevenue);
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([new(System.Security.Claims.ClaimTypes.NameIdentifier, userId)], "test"));
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IDataAccessScope>().ResolveAsync(principal, retained.SellerId, default));
    }

    [Theory]
    [InlineData("RODRIGO", "VENDEDOR: RODRIGO KEHL", "VENDEDOR: RODRIGO")]
    [InlineData("VENDEDOR: RODRIGO KEHL", "RODRIGO", "VENDEDOR: RODRIGO")]
    [InlineData("SUPERVISOR: RODRIGO KEHL", "VENDEDOR: RODRIGO", "VENDEDOR: RODRIGO")]
    [InlineData("VENDEDOR: RODRIGO", "SUPERVISOR: RODRIGO KEHL", "VENDEDOR: RODRIGO")]
    [InlineData("VENDEDOR: MARCELO DA ROSA", "MARCELO IVONEI DA ROSA", "VENDEDOR: MARCELO IVONEI DA ROSA")]
    [InlineData("MARCELO IVONEI DA ROSA", "VENDEDOR: MARCELO DA ROSA", "VENDEDOR: MARCELO IVONEI DA ROSA")]
    [InlineData("SUPERVISOR: VALDIR ZACARIAS", "VENDEDOR: VALDIR ZACARIAS", "VALDIR ZACARIAS")]
    [InlineData("VALDIR ZACARIAS", "VENDEDOR: VALDIR ZACARIAS", "VALDIR ZACARIAS")]
    public async Task Prefixed_alias_registration_persists_canonical_unique_identity_and_rejects_duplicate(string firstAlias, string secondAlias, string canonical)
    {
        await using var factory = CreateFactory();
        await SeedAsync(factory, "Administrador");
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var first = await client.PostAsJsonAsync("/api/v1/admin/sellers", new { name = "Primeiro", importedName = firstAlias });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var created = await first.Content.ReadFromJsonAsync<Seller>();
        Assert.Equal(canonical, created!.ImportedName);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/admin/sellers", new { name = "Segundo", importedName = secondAlias })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OroBiDbContext>();
        Assert.Equal(canonical, (await db.Sellers.SingleAsync(s => s.Id == created.Id)).ImportedName);
        Assert.Contains(db.Model.FindEntityType(typeof(Seller))!.GetIndexes(), index => index.IsUnique && index.Properties.Select(p => p.Name).SequenceEqual([nameof(Seller.ImportedName)]));
    }

    [Fact]
    public async Task Administrator_manages_links_permissions_activation_reset_and_audits_without_secrets()
    {
        await using var factory = CreateFactory();
        await SeedAsync(factory, "Administrador");
        using var admin = factory.CreateClient();
        await LoginAsync(admin);
        var sellerResponse = await admin.PostAsJsonAsync("/api/v1/admin/sellers", new { name = "Bia", importedName = "BIA" });
        Assert.Equal(HttpStatusCode.Created, sellerResponse.StatusCode);
        var seller = await sellerResponse.Content.ReadFromJsonAsync<Seller>();
        Assert.NotNull(seller);
        var accesses = new[] { new { sellerId = seller.Id, isActive = true, permissions = new SellerPortalPermissions { CanViewRevenue = false } } };
        var created = await admin.PostAsJsonAsync("/api/v1/admin/users", new { email = "bia@example.invalid", password = "Synthetic-789!", role = "Vendedor", sellerAccesses = accesses });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var account = await created.Content.ReadFromJsonAsync<CreatedUser>();
        Assert.NotNull(account);
        using var bia = factory.CreateClient();
        var login = await (await bia.PostAsJsonAsync("/api/v1/auth/login", new { email = "bia@example.invalid", password = "Synthetic-789!" })).Content.ReadFromJsonAsync<LocalLoginResult>();
        bia.DefaultRequestHeaders.Authorization = new("Bearer", login!.AccessToken);
        var profile = await (await bia.GetAsync("/api/v1/me")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(seller.Id, profile.GetProperty("sellerId").GetGuid());
        Assert.False(profile.GetProperty("permissions").GetProperty("canViewRevenue").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/admin/users/{account.Id}/access", new { role = "Vendedor", sellerAccesses = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/admin/users/{account.Id}/access", new { role = "Vendedor", sellerAccesses = new[] { new { sellerId = seller.Id, isActive = true, permissions = new SellerPortalPermissions { CanViewCommission = false } } } })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await bia.GetAsync("/api/v1/me")).StatusCode);
        login = await (await bia.PostAsJsonAsync("/api/v1/auth/login", new { email = "bia@example.invalid", password = "Synthetic-789!" })).Content.ReadFromJsonAsync<LocalLoginResult>();
        bia.DefaultRequestHeaders.Authorization = new("Bearer", login!.AccessToken);
        profile = await (await bia.GetAsync("/api/v1/me")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.False(profile.GetProperty("permissions").GetProperty("canViewCommission").GetBoolean());
        Assert.True(profile.GetProperty("permissions").GetProperty("canViewRevenue").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/admin/users/{account.Id}/active", new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await bia.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/v1/admin/users/{account.Id}/reset-password", new { newPassword = "Synthetic-456!" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/admin/users/{account.Id}/active", new { isActive = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await bia.PostAsJsonAsync("/api/v1/auth/login", new { email = "bia@example.invalid", password = "Synthetic-789!" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await bia.PostAsJsonAsync("/api/v1/auth/login", new { email = "bia@example.invalid", password = "Synthetic-456!" })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var audits = await scope.ServiceProvider.GetRequiredService<OroBiDbContext>().AccountAuditEvents.ToArrayAsync();
        Assert.Contains(audits, a => a.Action == "UserCreated" && a.TargetId == account.Id);
        Assert.Contains(audits, a => a.Action == "PasswordReset" && a.TargetId == account.Id);
        Assert.DoesNotContain(audits, a => (a.DetailsJson ?? string.Empty).Contains("Synthetic", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Alias_collisions_cannot_create_a_second_identity_for_same_imported_seller()
    {
        await using var factory = CreateFactory();
        await SeedAsync(factory, "Administrador");
        using var client = factory.CreateClient();
        await LoginAsync(client);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/admin/sellers", new { name = "Rodrigo Kehl", importedName = "RODRIGO" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/admin/sellers", new { name = "Outro", importedName = "VENDEDOR: RODRIGO" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/admin/sellers", new { name = "Valdir", importedName = "VALDIR ZACARIAS" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/admin/sellers", new { name = "Outro", importedName = "VENDEDOR: VALDIR ZACARIAS" })).StatusCode);
    }

    private sealed record CreatedUser(string Id);

    [Fact]
    public async Task Logout_revokes_real_bearer_token_and_login_token_is_not_cached()
    {
        await using var factory = CreateFactory();
        await SeedAsync(factory);
        using var client = factory.CreateClient();
        await LoginAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Deactivated_user_loses_existing_token_and_cannot_login()
    {
        await using var factory = CreateFactory();
        var id = await SeedAsync(factory);
        using var client = factory.CreateClient();
        await LoginAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OroBiDbContext>();
            (await db.Users.SingleAsync(user => user.Id == id)).IsActive = false;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "seller@example.invalid", password = "Synthetic-123!" })).StatusCode);
    }

    [Fact]
    public async Task Persisted_role_change_removes_privilege_of_existing_token()
    {
        await using var factory = CreateFactory();
        var id = await SeedAsync(factory, "Administrador");
        using var client = factory.CreateClient();
        await LoginAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/admin/users")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await manager.FindByIdAsync(id))!;
            Assert.True((await manager.RemoveFromRoleAsync(user, "Administrador")).Succeeded);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/users")).StatusCode);
    }

    [Fact]
    public async Task Change_password_revokes_old_session_and_accepts_only_new_password()
    {
        await using var factory = CreateFactory();
        await SeedAsync(factory);
        using var client = factory.CreateClient();
        await LoginAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/me/change-password", new { currentPassword = "Synthetic-123!", newPassword = "Synthetic-456!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "seller@example.invalid", password = "Synthetic-123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "seller@example.invalid", password = "Synthetic-456!" })).StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var database = Guid.NewGuid().ToString();
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
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
    }

    private static async Task<string> SeedAsync(WebApplicationFactory<Program> factory, string role = "Vendedor")
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);
        var user = new ApplicationUser { UserName = "seller@example.invalid", Email = "seller@example.invalid" };
        Assert.True((await manager.CreateAsync(user, "Synthetic-123!")).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        var db = scope.ServiceProvider.GetRequiredService<OroBiDbContext>();
        var seller = new Seller { Name = "Ana", ImportedName = "ANA" };
        db.Sellers.Add(seller);
        db.UserSellerAccesses.Add(new UserSellerAccess { UserId = user.Id, SellerId = seller.Id });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "seller@example.invalid", password = "Synthetic-123!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        var login = await response.Content.ReadFromJsonAsync<LocalLoginResult>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
    }
}
