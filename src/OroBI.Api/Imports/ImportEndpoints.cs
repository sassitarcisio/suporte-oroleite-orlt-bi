using OroBI.Application.Imports;
using OroBI.Api.Auth;
using OroBI.Domain.Imports;

namespace OroBI.Api.Imports;

public static class ImportEndpoints
{
    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder endpoints, string prefix = "/api")
    {
        endpoints.MapPost($"{prefix}/imports", async (HttpRequest request, IImportWorkflow workflow, CancellationToken cancellationToken) =>
        {
            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file is null)
            {
                return Results.BadRequest(new { error = "A CSV file is required." });
            }

            if (!Enum.TryParse<ImportFileType>(form["fileType"], ignoreCase: true, out var fileType))
            {
                return Results.BadRequest(new { error = "A valid fileType is required." });
            }

            await using var content = file.OpenReadStream();
            var result = await workflow.ImportAsync(
                new ImportSubmission(fileType, file.FileName, file.ContentType, content),
                cancellationToken);
            return Results.Created($"{prefix}/imports", result);
        }).RequireAuthorization(AuthorizationPolicies.AdministratorOnly);

        return endpoints;
    }
}
