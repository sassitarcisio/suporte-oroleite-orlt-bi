using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OroBI.Application.Abstractions;
using OroBI.Infrastructure.Imports;

namespace OroBI.Infrastructure.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void Uses_blob_store_when_blob_service_uri_and_container_are_configured()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["ConnectionStrings:OroBi"] = "Host=localhost;Database=orobi",
            ["ImportStorage:BlobServiceUri"] = "https://orobistore.blob.core.windows.net",
            ["ImportStorage:ContainerName"] = "imports"
        });

        Assert.IsType<BlobImportFileStore>(provider.GetRequiredService<IImportFileStore>());
    }

    [Fact]
    public void Uses_local_store_when_blob_configuration_is_absent()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["ConnectionStrings:OroBi"] = "Host=localhost;Database=orobi",
            ["ImportStorage:LocalPath"] = "imports"
        });

        Assert.IsType<LocalImportFileStore>(provider.GetRequiredService<IImportFileStore>());
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddOroBiInfrastructure(configuration);
        return services.BuildServiceProvider();
    }
}
