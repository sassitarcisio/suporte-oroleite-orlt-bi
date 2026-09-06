using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Api.IntegrationTests.Auth;

public sealed partial class PortalSessionTests
{
    [Theory]
    [InlineData("/api")]
    [InlineData("/api/v1")]
    public async Task Persisted_first_access_flag_applies_to_existing_token_and_rejects_unsafe_methods(string prefix)
    {
        await using var factory = CreateFactory();
        var id = await SeedAsync(factory, "Administrador");
        using var client = factory.CreateClient();
        await LoginAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OroBiDbContext>();
            (await db.Users.SingleAsync(x => x.Id == id)).MustChangePassword = true;
            await db.SaveChangesAsync();
        }
        var me = await (await client.GetAsync(prefix + "/me")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(me.GetProperty("mustChangePassword").GetBoolean());
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
        {
            using var request = new HttpRequestMessage(method, prefix + "/admin/users");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(prefix + "/me", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(prefix + "/me/change-password")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(prefix + "/me/change-password", new { currentPassword = "Wrong-123!", newPassword = "Personal-456!" })).StatusCode);
        me = await (await client.GetAsync(prefix + "/me")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(me.GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(prefix.ToUpperInvariant() + "/AUTH/LOGOUT/", null)).StatusCode);
    }
}
