using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OroBI.Infrastructure.Identity;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Tests.Identity;

public sealed class LocalAuthenticationServiceTests : IDisposable
{
    private const string Email = "synthetic@example.invalid";
    private const string Password = "Synthetic-Test-123!";
    private readonly OroBiDbContext _context;
    private readonly UserManager<ApplicationUser> _manager;
    private readonly LocalAuthenticationService _service;

    public LocalAuthenticationServiceTests()
    {
        _context = new OroBiDbContext(new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _manager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser, IdentityRole, OroBiDbContext>(_context),
            null!, new PasswordHasher<ApplicationUser>(), [], [], new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(), null!, NullLogger<UserManager<ApplicationUser>>.Instance);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "test", ["Jwt:Audience"] = "test",
            ["Jwt:SigningKey"] = "synthetic-test-signing-key-with-thirty-two-characters"
        }).Build();
        _service = new LocalAuthenticationService(_manager, config, _context);
    }

    [Fact]
    public async Task Token_contains_persisted_session_version()
    {
        var user = await CreateUserAsync();
        var result = await _service.LoginAsync(Email, Password, default);
        Assert.NotNull(result);
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        Assert.Equal(user.SecurityStamp, token.Claims.SingleOrDefault(claim => claim.Type == "session_version")?.Value);
    }

    [Fact]
    public async Task Login_audit_records_outcomes_without_credentials()
    {
        await CreateUserAsync();
        await _service.LoginAsync(Email, "Wrong-Synthetic-Password!", default);
        await _service.LoginAsync(Email, Password, default);
        var events = await _context.AccountAuditEvents.OrderBy(item => item.OccurredAtUtc).ToArrayAsync();
        Assert.Equal(new[] { "LoginFailed", "LoginSucceeded" }, events.Select(item => item.Action));
        var json = System.Text.Json.JsonSerializer.Serialize(events);
        Assert.DoesNotContain(Password, json);
        Assert.DoesNotContain("Wrong-Synthetic-Password!", json);
        Assert.DoesNotContain("accessToken", json);
    }

    [Fact]
    public async Task Locked_account_cannot_obtain_token_even_with_correct_password()
    {
        var user = await CreateUserAsync();
        Assert.True((await _manager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddHours(1))).Succeeded);
        Assert.True(await _manager.IsLockedOutAsync(user));

        Assert.Null(await _service.LoginAsync(Email, Password, default));
    }

    [Fact]
    public async Task Invalid_passwords_count_toward_lockout_and_then_block_correct_password()
    {
        var user = await CreateUserAsync();
        Assert.Null(await _service.LoginAsync(Email, "incorrect", default));
        Assert.Equal(1, await _manager.GetAccessFailedCountAsync(user));
        for (var attempt = 1; attempt < _manager.Options.Lockout.MaxFailedAccessAttempts; attempt++)
            Assert.Null(await _service.LoginAsync(Email, "incorrect", default));

        Assert.True(await _manager.IsLockedOutAsync(user));
        Assert.Null(await _service.LoginAsync(Email, Password, default));
    }

    [Fact]
    public async Task Successful_login_resets_failure_count_and_accepts_normalized_email()
    {
        var user = await CreateUserAsync();
        Assert.True((await _manager.AccessFailedAsync(user)).Succeeded);

        Assert.NotNull(await _service.LoginAsync($"  {Email.ToUpperInvariant()}  ", Password, default));

        Assert.Equal(0, await _manager.GetAccessFailedCountAsync(user));
    }

    [Fact]
    public async Task Expired_lockout_allows_correct_password_again()
    {
        var user = await CreateUserAsync();
        Assert.True((await _manager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(-1))).Succeeded);

        Assert.NotNull(await _service.LoginAsync(Email, Password, default));
    }

    [Theory]
    [InlineData("missing@example.invalid", "incorrect")]
    [InlineData("", "incorrect")]
    [InlineData("synthetic@example.invalid", "")]
    public async Task Unavailable_credentials_have_same_unauthenticated_result(string email, string password)
    {
        await CreateUserAsync();
        Assert.Null(await _service.LoginAsync(email, password, default));
    }

    private async Task<ApplicationUser> CreateUserAsync()
    {
        var user = new ApplicationUser { UserName = Email, Email = Email, LockoutEnabled = true };
        Assert.True((await _manager.CreateAsync(user, Password)).Succeeded);
        return user;
    }

    public void Dispose()
    {
        _manager.Dispose();
        _context.Dispose();
    }
}
