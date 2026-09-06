using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OroBI.Domain.Sellers;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Api.IntegrationTests.Auth;

public sealed partial class PortalSessionTests
{
    [Theory]
    [InlineData("/api")]
    [InlineData("/api/v1")]
    public async Task Administrative_password_requires_change_before_any_private_data(string prefix)
    {
        await using var factory = CreateFactory();
        await SeedAsync(factory, "Administrador");
        using var admin = factory.CreateClient();
        await LoginAsync(admin);
        var created = await admin.PostAsJsonAsync("/api/v1/admin/users", new { email = "first@example.invalid", password = "Temporary-123!", role = "Administrador", name = " Pessoa ", sellerAccesses = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(prefix + "/auth/login", new { email = "first@example.invalid", password = "Temporary-123!" });
        var login = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(login.GetProperty("mustChangePassword").GetBoolean());
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.GetProperty("accessToken").GetString());
        var me = await (await client.GetAsync(prefix.ToUpperInvariant() + "/ME/")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(me.GetProperty("mustChangePassword").GetBoolean());
        foreach (var path in new[] { "/me/dashboard", "/dashboard", "/admin/users", "/management/sellers", "/future-private-resource" })
        {
            var denied = await client.GetAsync(prefix.ToUpperInvariant() + path + "/");
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            var body = await denied.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("password_change_required", body.GetProperty("code").GetString());
            Assert.Equal("Crie uma nova senha para continuar.", body.GetProperty("error").GetString());
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(prefix + "/me/change-password", new { currentPassword = "Temporary-123!", newPassword = "Temporary-123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(prefix.ToUpperInvariant() + "/ME/CHANGE-PASSWORD/", new { currentPassword = "Temporary-123!", newPassword = "Personal-456!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(prefix + "/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        login = await (await client.PostAsJsonAsync(prefix + "/auth/login", new { email = "first@example.invalid", password = "Personal-456!" })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(login.GetProperty("mustChangePassword").GetBoolean());
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/admin/users")).StatusCode);
        var userId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/v1/admin/users/{userId}/reset-password", new { newPassword = "Reset-789!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(prefix + "/me")).StatusCode);
        login = await (await client.PostAsJsonAsync(prefix + "/auth/login", new { email = "first@example.invalid", password = "Reset-789!" })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(login.GetProperty("mustChangePassword").GetBoolean());
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(prefix + "/auth/logout", null)).StatusCode);
    }

    [Fact]
    public async Task Generated_credentials_are_returned_once_and_reset_requires_change_again()
    {
        await using var factory = CreateFactory();
        await SeedAsync(factory, "Administrador");
        using var admin = factory.CreateClient();
        await LoginAsync(admin);
        var response = await admin.PostAsJsonAsync("/api/v1/admin/users", new { email = "generated@example.invalid", generateTemporaryPassword = true, name = " Nome Pessoa ", role = "Diretoria", sellerAccesses = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var password = created.GetProperty("temporaryPassword").GetString()!;
        Assert.True(password.Length >= 16);
        var id = created.GetProperty("id").GetString();
        using var client = factory.CreateClient();
        var login = await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "generated@example.invalid", password })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(login.GetProperty("mustChangePassword").GetBoolean());
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/me/change-password", new { currentPassword = password, newPassword = "Personal-456!" })).StatusCode);
        response = await admin.PostAsJsonAsync($"/api/v1/admin/users/{id}/reset-password", new { generateTemporaryPassword = true });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var resetPassword = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("temporaryPassword").GetString()!;
        Assert.NotEqual(password, resetPassword);
        login = await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "generated@example.invalid", password = resetPassword })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(login.GetProperty("mustChangePassword").GetBoolean());
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/API/V1/AUTH/LOGOUT/", null)).StatusCode);
        var accounts = await (await admin.GetAsync("/api/v1/admin/users")).Content.ReadFromJsonAsync<JsonElement>();
        var account = accounts.EnumerateArray().Single(x => x.GetProperty("id").GetString() == id);
        Assert.Equal("Nome Pessoa", account.GetProperty("registrationName").GetString());
        Assert.True(account.GetProperty("mustChangePassword").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, account.GetProperty("lastLoginAtUtc").ValueKind);
        Assert.False(account.TryGetProperty("temporaryPassword", out _));
        using var scope = factory.Services.CreateScope();
        var audit = JsonSerializer.Serialize(await scope.ServiceProvider.GetRequiredService<OroBiDbContext>().AccountAuditEvents.ToArrayAsync());
        Assert.DoesNotContain(password, audit);
        Assert.DoesNotContain(resetPassword, audit);
    }

    [Theory]
    [InlineData("inactive-seller", "Vendedor")]
    [InlineData("inactive-link", "Vendedor")]
    [InlineData("missing-link", "Vendedor")]
    [InlineData("duplicate-link", "Vendedor")]
    [InlineData("inactive-seller", "Administrador")]
    [InlineData("inactive-seller", "Diretoria")]
    public async Task Seller_eligibility_is_rechecked_for_login_and_existing_token(string state, string role)
    {
        await using var factory = CreateFactory();
        var id = await SeedAsync(factory, role);
        using var client = factory.CreateClient();
        await LoginAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OroBiDbContext>();
            var link = await db.UserSellerAccesses.Include(x => x.Seller).SingleAsync(x => x.UserId == id);
            if (state == "inactive-seller") link.Seller.IsActive = false;
            if (state == "inactive-link") link.IsActive = false;
            if (state == "missing-link") db.UserSellerAccesses.Remove(link);
            if (state == "duplicate-link")
            {
                var other = new Seller { Name = "Outro", ImportedName = "OUTRO" };
                db.Sellers.Add(other);
                db.UserSellerAccesses.Add(new() { UserId = id, SellerId = other.Id });
            }
            await db.SaveChangesAsync();
        }
        var expected = role == "Vendedor" ? HttpStatusCode.Unauthorized : HttpStatusCode.OK;
        Assert.Equal(expected, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(expected, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "seller@example.invalid", password = "Synthetic-123!" })).StatusCode);
    }

    [Fact]
    public async Task External_code_is_optional_normalized_unique_and_edit_preserves_identity()
    {
        await using var factory = CreateFactory();
        await SeedAsync(factory, "Administrador");
        using var admin = factory.CreateClient();
        await LoginAsync(admin);
        var response = await admin.PostAsJsonAsync("/api/v1/admin/sellers", new { name = "Bia", importedName = "BIA", externalId = " erp-123 " });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var seller = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = seller.GetProperty("id").GetGuid();
        Assert.Equal("ERP-123", seller.GetProperty("externalId").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/admin/sellers", new { name = "Cris", importedName = "CRIS", externalId = "Erp-123" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/admin/sellers/{id}/external-id", new { externalId = new string('X', 65) })).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var existing = await scope.ServiceProvider.GetRequiredService<OroBiDbContext>().Sellers.SingleAsync(x => x.Name == "Ana");
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/admin/sellers/{existing.Id}/external-id", new { externalId = "SECOND" })).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/admin/sellers/{id}/external-id", new { externalId = " second " })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/admin/sellers/{id}/external-id", new { externalId = " edited " })).StatusCode);
        var sellers = await (await admin.GetAsync("/api/v1/admin/sellers")).Content.ReadFromJsonAsync<JsonElement>();
        seller = sellers.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == id);
        Assert.Equal("EDITED", seller.GetProperty("externalId").GetString());
        Assert.Equal("Bia", seller.GetProperty("name").GetString());
        Assert.Equal("BIA", seller.GetProperty("importedName").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/admin/sellers/{id}/external-id", new { externalId = (string?)null })).StatusCode);
    }
}
