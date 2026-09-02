using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OroBI.Application.Abstractions;
using OroBI.Application.Imports;
using OroBI.Domain.Imports;

namespace OroBI.Api.IntegrationTests.Imports;

public sealed class ImportEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ImportEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:OroBi"] = "Host=localhost;Database=orobi"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
                services.RemoveAll<IImportWorkflow>();
                services.AddScoped<IImportWorkflow, TestImportWorkflow>();
            });
        }).CreateClient();
    }

    [Fact]
    public async Task Post_imports_without_file_returns_bad_request()
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Power"), "fileType");

        var response = await _client.PostAsync("/api/imports", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"Status: {response.StatusCode}; Body: {responseBody}");
    }

    [Fact]
    public async Task Post_imports_with_valid_power_file_returns_created()
    {
        const string csv = "DATA;VENDEDOR;MARCA;GRUPO;TIPO;CIDADE;NOME;PRODUTO;VALTOTAL;QTDE;PRECOCUSTO;CODCLIENTE;NRODOCUMENTO\n";
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Power"), "fileType");
        content.Add(new StringContent(csv), "file", "power.csv");

        var response = await _client.PostAsync("/api/imports", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Status: {response.StatusCode}; Body: {responseBody}");
        Assert.Contains("storedFileUri", responseBody, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class TestImportWorkflow : IImportWorkflow
{
    public Task<ImportExecutionResult> ImportAsync(ImportSubmission submission, CancellationToken cancellationToken) =>
        Task.FromResult(new ImportExecutionResult(ImportBatchStatus.Completed, "memory://power.csv", 0, 0));
}
