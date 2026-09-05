using Azure.Identity;
using Azure.Storage.Blobs;
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
using OroBI.Application.Portal;
using OroBI.Infrastructure.Portal;

namespace OroBI.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOroBiInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OroBi")
            ?? throw new InvalidOperationException("Connection string 'OroBi' is required.");
        var importRootPath = configuration["ImportStorage:LocalPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "imports");
        var blobServiceUri = configuration["ImportStorage:BlobServiceUri"];
        var blobContainerName = configuration["ImportStorage:ContainerName"];

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
        if (!string.IsNullOrWhiteSpace(blobServiceUri) && !string.IsNullOrWhiteSpace(blobContainerName))
        {
            var containerClient = new BlobServiceClient(new Uri(blobServiceUri), new DefaultAzureCredential())
                .GetBlobContainerClient(blobContainerName);
            services.AddSingleton<IBlobImportUploader>(new AzureBlobImportUploader(containerClient));
            services.AddSingleton<IImportFileStore>(serviceProvider => new BlobImportFileStore(
                serviceProvider.GetRequiredService<IBlobImportUploader>(),
                TimeProvider.System));
        }
        else
        {
            services.AddSingleton<IImportFileStore>(_ => new LocalImportFileStore(importRootPath));
        }
        services.AddScoped<IImportWorkflow, CsvImportWorkflow>();
        services.AddScoped<ILocalAuthenticationService, LocalAuthenticationService>();
        services.AddScoped<InitialAdminProvisioner>();
        services.AddScoped<ISellerClosingQueryService, SellerClosingQueryService>();
        services.AddScoped<IPayrollClosingQueryService, SellerClosingQueryService>();
        services.AddScoped<IDashboardQueryService, DashboardQueryService>();
        services.AddScoped<ICommercialAnalyticsQueryService, DashboardQueryService>();
        services.AddScoped<ICommercialFilterOptionsQueryService, CommercialFilterOptionsQueryService>();
        services.AddScoped<IPortalQueryService, PortalQueryService>();
        services.AddScoped<IPortalClosingService, PortalClosingService>();
        return services;
    }
}
