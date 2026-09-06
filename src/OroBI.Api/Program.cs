using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OroBI.Api;
using OroBI.Api.Auth;
using OroBI.Api.Closings;
using OroBI.Api.Imports;
using OroBI.Api.Migrations;
using OroBI.Api.Analytics;
using OroBI.Api.Portal;
using OroBI.Infrastructure;
using OroBI.Infrastructure.Identity;
using OroBI.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOroBiInfrastructure(builder.Configuration);
builder.Services.AddSellerPortalIdentity();
builder.Services.Configure<IdentityOptions>(options =>
{
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
});
builder.Services.AddSingleton<LoginRateLimiter>();
builder.Services.AddSingleton<RegistrationRateLimiter>();
builder.Services.AddOptions<BrowserSessionOptions>().Bind(builder.Configuration.GetSection(BrowserSessionOptions.SectionName))
    .Validate(options => options.IsValid(), "BrowserSession requires a DNS API host and an exact HTTPS origin allowlist without paths or wildcards.")
    .ValidateOnStart();

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase) ||
    args.Contains("--provision-admin", StringComparer.OrdinalIgnoreCase))
{
    var migrationApp = builder.Build();
    using var scope = migrationApp.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<OroBiDbContext>();
    var connectionString = dbContext.Database.GetDbConnection().ConnectionString;
    var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database;
    if (string.IsNullOrWhiteSpace(databaseName))
    {
        throw new InvalidOperationException("Connection string database name is required for migrations.");
    }

    await using var administrativeConnection = new NpgsqlConnection(MigrationConnectionFactory.CreateAdministrativeConnection(connectionString));
    await administrativeConnection.OpenAsync();
    await using var existsCommand = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", administrativeConnection);
    existsCommand.Parameters.AddWithValue("name", databaseName);
    if (await existsCommand.ExecuteScalarAsync() is null)
    {
        await using var createCommand = new NpgsqlCommand($"CREATE DATABASE \"{databaseName.Replace("\"", "\"\"")}\"", administrativeConnection);
        await createCommand.ExecuteNonQueryAsync();
    }

    await dbContext.Database.MigrateAsync();

    if (args.Contains("--provision-admin", StringComparer.OrdinalIgnoreCase))
    {
        var administrators = builder.Configuration.GetSection("InitialAdmins")
            .Get<InitialAdminCredential[]>() ?? [];
        var provisioner = scope.ServiceProvider.GetRequiredService<InitialAdminProvisioner>();
        await provisioner.ProvisionAsync(administrators, CancellationToken.None);
    }

    return;
}

var jwtSection = builder.Configuration.GetRequiredSection(JwtOptions.SectionName);
builder.Services.AddOptions<JwtOptions>().Bind(jwtSection).ValidateDataAnnotations().ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? throw new InvalidOperationException("JWT configuration is required.");
        options.Events = new JwtBearerEvents { OnMessageReceived = BrowserSession.ReceiveTokenAsync, OnTokenValidated = SessionTokenValidation.ValidateAsync };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.AdministratorOnly, policy => policy.RequireRole("Administrador"));
    options.AddPolicy(AuthorizationPolicies.ManagerOrAdministrator, policy => policy.RequireRole("Administrador", "Diretoria"));
    options.AddPolicy(AuthorizationPolicies.SellerScope, policy =>
        policy.RequireAuthenticatedUser().RequireRole("Administrador", "Diretoria", "Gestor", "Gerente", "Vendedor"));
});

builder.Services.AddCors(options => options.AddPolicy("Web", policy =>
{
    var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
    if (corsOrigins.Length > 0)
    {
        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
    }
}));

builder.Services.AddCors(options => options.AddPolicy("BrowserSession", policy =>
{
    var browserSessionOptions = builder.Configuration.GetSection(BrowserSessionOptions.SectionName).Get<BrowserSessionOptions>() ?? new();
    if (browserSessionOptions.Enabled && browserSessionOptions.IsValid())
        policy.WithOrigins(browserSessionOptions.AllowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));
var app = builder.Build();
var browserSessionOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<BrowserSessionOptions>>().Value;

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        context.Response.Headers.CacheControl = "no-store";
    await next(context);
});
app.UseWhen(context => BrowserSession.IsAllowedOrigin(context.Request, browserSessionOptions), branch => branch.UseCors("BrowserSession"));
app.UseWhen(context => !BrowserSession.IsAllowedOrigin(context.Request, browserSessionOptions), branch => branch.UseCors("Web"));
app.UseMiddleware<BrowserSessionGuardMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseMiddleware<RequiredPasswordChangeMiddleware>();
app.UseAuthorization();
app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapAuthEndpoints("/api/v1");
app.MapSellerPortalAccountEndpoints();
app.MapCurrentUserEndpoints();
app.MapCurrentUserEndpoints("/api/v1");
app.MapImportEndpoints();
app.MapImportEndpoints("/api/v1");
app.MapDashboardEndpoints();
app.MapDashboardEndpoints("/api/v1");
app.MapDashboardFilterOptionsEndpoints();
app.MapDashboardFilterOptionsEndpoints("/api/v1");
app.MapSellerEndpoints();
app.MapSellerEndpoints("/api/v1");
app.MapCommercialAnalyticsEndpoints();
app.MapCommercialAnalyticsEndpoints("/api/v1");
app.MapClosingEndpoints();
app.MapClosingEndpoints("/api/v1");
app.MapPortalEndpoints();

app.Run();

public partial class Program;
