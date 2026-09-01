using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OroBI.Application.Abstractions;
using OroBI.Application.Imports;
using OroBI.Application.Identity;
using OroBI.Application.Analytics;
using OroBI.Infrastructure.Analytics;
using OroBI.Infrastructure.Imports;
using OroBI.Infrastructure.Closings;
using OroBI.Application.Closings;
using OroBI.Infrastructure.Identity;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOroBiInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OroBi")
            ?? throw new InvalidOperationException("Connection string 'OroBi' is required.");
        var importRootPath = configuration["ImportStorage:LocalPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "imports");

        services.AddDbContext<OroBiDbContext>(options => options.UseNpgsql(connectionString));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
        })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<OroBiDbContext>();
        services.AddSingleton<IImportFileStore>(_ => new LocalImportFileStore(importRootPath));
        services.AddScoped<IImportWorkflow, CsvImportWorkflow>();
        services.AddScoped<ILocalAuthenticationService, LocalAuthenticationService>();
        services.AddScoped<ISellerClosingQueryService, SellerClosingQueryService>();
        services.AddScoped<IDashboardQueryService, DashboardQueryService>();
        services.AddScoped<ICommercialAnalyticsQueryService, DashboardQueryService>();
        return services;
    }
}
