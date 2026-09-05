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
using OroBI.Domain.Sellers;
using OroBI.Infrastructure.Identity;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Api.IntegrationTests.Auth;

public sealed class SelfRegistrationTests
{
    private const string Password = "Synthetic-123!";

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Admin_bootstrap_never_promotes_self_registered_origin_without_explicit_existing_admin(bool approved, bool alreadyAdmin)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Accepted, (await RegisterAsync(client)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var user = (await users.FindByEmailAsync("pending@example.invalid"))!;
        if (approved)
        {
            user.IsRegistrationPending = false; user.IsActive = true;
            Assert.True((await users.UpdateAsync(user)).Succeeded);
            var role = alreadyAdmin ? "Administrador" : "Vendedor";
            Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        }
        var provisioner = scope.ServiceProvider.GetRequiredService<InitialAdminProvisioner>();
        var credentials = new[] { new InitialAdminCredential("pending@example.invalid", "Configured-456!") };
        if (alreadyAdmin) await provisioner.ProvisionAsync(credentials, default);
        else await Assert.ThrowsAsync<InvalidOperationException>(() => provisioner.ProvisionAsync(credentials, default));
        Assert.Equal(alreadyAdmin, await users.IsInRoleAsync(user, "Administrador"));
        Assert.True(await users.CheckPasswordAsync(user, Password));
    }

    [Fact]
    public async Task Public_registration_stays_inactive_without_privileges_and_cannot_login()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { name = "  Pessoa Sintética  ", email = "pending@example.invalid", password = Password, role = "Administrador", sellerId = Guid.NewGuid(), isActive = true, permissions = new SellerPortalPermissions() });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("message", out _));
        Assert.False(body.TryGetProperty("accessToken", out _));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OroBiDbContext>();
        var user = await db.Users.SingleAsync();
        Assert.False(user.IsActive);
        Assert.Empty(await db.UserRoles.ToArrayAsync());
        Assert.Empty(await db.UserSellerAccesses.ToArrayAsync());
        var audit = Assert.Single(await db.AccountAuditEvents.ToArrayAsync());
        Assert.Equal("RegistrationRequested", audit.Action);
        Assert.DoesNotContain(Password, audit.DetailsJson ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = Password })).StatusCode);
    }

    [Fact]
    public async Task Duplicate_normalized_email_returns_same_acceptance_and_preserves_existing_account()
    {
        await using var factory = CreateFactory();
        var existingId = await SeedUserAsync(factory, "existing@example.invalid", "Administrador");
        using var client = factory.CreateClient();
        var first = await RegisterAsync(client, "new@example.invalid");
        var duplicate = await client.PostAsJsonAsync("/api/v1/auth/register", new { name = "Pretensão diferente", email = " EXISTING@EXAMPLE.INVALID ", password = "Different-456!" });
        Assert.Equal(HttpStatusCode.Accepted, duplicate.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await duplicate.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = (await users.FindByIdAsync(existingId))!;
        Assert.True(existing.IsActive);
        Assert.True(await users.IsInRoleAsync(existing, "Administrador"));
        Assert.True(await users.CheckPasswordAsync(existing, Password));
        Assert.False(await users.CheckPasswordAsync(existing, "Different-456!"));
    }

    [Fact]
    public async Task Administrator_approves_pending_registration_into_exact_seller_scope_once()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Accepted, (await RegisterAsync(client)).StatusCode);
        var adminId = await SeedUserAsync(factory, "admin@example.invalid", "Administrador");
        _ = adminId;
        await LoginAsync(client, "admin@example.invalid");
        string pendingId;
        Guid sellerId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OroBiDbContext>();
            pendingId = (await db.Users.SingleAsync(u => u.Email == "pending@example.invalid")).Id;
            var seller = new Seller { Name = "Ana", ImportedName = "ANA" };
            db.Sellers.Add(seller); await db.SaveChangesAsync(); sellerId = seller.Id;
        }
        var listed = await (await client.GetAsync("/api/v1/admin/users")).Content.ReadFromJsonAsync<JsonElement>();
        var pending = listed.EnumerateArray().Single(u => u.GetProperty("id").GetString() == pendingId);
        Assert.Equal("Pessoa Sintética", pending.GetProperty("registrationName").GetString());
        Assert.True(pending.GetProperty("isRegistrationPending").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/v1/admin/users/{pendingId}/active", new { isActive = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/v1/admin/users/{pendingId}/access", new { role = "Administrador", sellerAccesses = Array.Empty<object>() })).StatusCode);
        var approval = new { sellerId, permissions = new SellerPortalPermissions { CanViewRevenue = false } };
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/v1/admin/users/{pendingId}/approve-registration", approval)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/v1/admin/users/{pendingId}/approve-registration", approval)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(pendingId))!;
            Assert.True(user.IsActive);
            Assert.Equal(["Vendedor"], await users.GetRolesAsync(user));
            var db = scope.ServiceProvider.GetRequiredService<OroBiDbContext>();
            var access = Assert.Single(await db.UserSellerAccesses.Where(a => a.UserId == pendingId && a.IsActive).ToArrayAsync());
            Assert.Equal(sellerId, access.SellerId); Assert.False(access.Permissions.CanViewRevenue);
            Assert.Single(await db.AccountAuditEvents.Where(a => a.Action == "RegistrationApproved" && a.TargetId == pendingId).ToArrayAsync());
        }
        using var approvedClient = factory.CreateClient();
        await LoginAsync(approvedClient, "pending@example.invalid");
        var profile = await (await approvedClient.GetAsync("/api/v1/me")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(sellerId, profile.GetProperty("sellerId").GetGuid());
        Assert.False(profile.GetProperty("permissions").GetProperty("canViewRevenue").GetBoolean());
    }

    [Theory]
    [InlineData("Vendedor")]
    [InlineData("Gestor")]
    [InlineData("Diretoria")]
    [InlineData(null)]
    public async Task Restricted_accounts_and_anonymous_callers_cannot_approve(string? role)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client);
        if (role is not null) { await SeedUserAsync(factory, "restricted@example.invalid", role); await LoginAsync(client, "restricted@example.invalid"); }
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<OroBiDbContext>().Users.SingleAsync(u => u.Email == "pending@example.invalid");
        Assert.Equal(role is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/v1/admin/users/{user.Id}/approve-registration", new { sellerId = Guid.NewGuid() })).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_inactive_seller_approval_never_activates_account(bool sellerExists)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client); await SeedUserAsync(factory, "admin@example.invalid", "Administrador"); await LoginAsync(client, "admin@example.invalid");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OroBiDbContext>();
        var seller = new Seller { Name = "Inactive", ImportedName = "INACTIVE", IsActive = false };
        if (sellerExists) { db.Sellers.Add(seller); await db.SaveChangesAsync(); }
        var user = await db.Users.SingleAsync(u => u.Email == "pending@example.invalid");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/v1/admin/users/{user.Id}/approve-registration", new { sellerId = seller.Id })).StatusCode);
        await db.Entry(user).ReloadAsync(); Assert.False(user.IsActive);
        Assert.Empty(await db.UserRoles.Where(r => r.UserId == user.Id).ToArrayAsync());
        Assert.Empty(await db.UserSellerAccesses.Where(a => a.UserId == user.Id).ToArrayAsync());
    }

    [Fact]
    public async Task Registration_has_both_account_and_overall_throttling()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        for (var index = 0; index < 3; index++) Assert.Equal(HttpStatusCode.Accepted, (await RegisterAsync(client)).StatusCode);
        var blocked = await RegisterAsync(client, " PENDING@EXAMPLE.INVALID ");
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.NotNull(blocked.Headers.RetryAfter);
        var throttledGlobally = false;
        for (var index = 0; index < 65; index++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { name = "Pessoa Sintética", email = $"unique{index}@example.invalid", password = "weak" });
            if (response.StatusCode == HttpStatusCode.TooManyRequests) { throttledGlobally = true; break; }
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.True(throttledGlobally);
    }

    [Theory]
    [InlineData("", "valid@example.invalid", "Synthetic-123!")]
    [InlineData("Pessoa", "invalid", "Synthetic-123!")]
    [InlineData("Pessoa", "valid@example.invalid", "weakpass")]
    public async Task Invalid_registration_is_rejected_without_creating_account(string name, string email, string password)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/register", new { name, email, password })).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<OroBiDbContext>().Users.ToArrayAsync());
    }

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email = "pending@example.invalid") => client.PostAsJsonAsync("/api/v1/auth/register", new { name = "Pessoa Sintética", email, password = Password });
    private static WebApplicationFactory<Program> CreateFactory()
    {
        var database = Guid.NewGuid().ToString();
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.ConfigureLogging(logging => logging.ClearProviders()); builder.ConfigureServices(services =>
        {
            services.RemoveAll<OroBiDbContext>(); services.RemoveAll<DbContextOptions<OroBiDbContext>>(); services.RemoveAll<IDbContextOptionsConfiguration<OroBiDbContext>>();
            services.AddDbContext<OroBiDbContext>(options => options.UseInMemoryDatabase(database));
        }); });
    }
    private static async Task<string> SeedUserAsync(WebApplicationFactory<Program> factory, string email, string role)
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync(role)) Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);
        var user = new ApplicationUser { UserName = email, Email = email };
        Assert.True((await manager.CreateAsync(user, Password)).Succeeded); Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        return user.Id;
    }
    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LocalLoginResult>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result!.AccessToken);
    }
}
