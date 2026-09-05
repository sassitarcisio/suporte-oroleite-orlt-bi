using System.Globalization;
using System.Security.Claims;
using OroBI.Api.Auth;
using Microsoft.EntityFrameworkCore;
using OroBI.Application.Closings;
using OroBI.Application.Analytics;
using OroBI.Domain.Closings;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Api.Closings;

public static class ClosingEndpoints
{
    public static IEndpointRouteBuilder MapClosingEndpoints(this IEndpointRouteBuilder endpoints, string prefix = "/api")
    {
        endpoints.MapPost($"{prefix}/closing-configurations", async (ClosingConfigurationRequest request, OroBiDbContext dbContext, CancellationToken cancellationToken) =>
        {
            if (request.Month is < 1 or > 12) return Results.BadRequest(new { error = "month must be between 1 and 12." });
            var exists = await dbContext.SellerClosingConfigurations.AnyAsync(item => item.Seller == request.Seller.Trim().ToUpperInvariant() && item.Year == request.Year && item.Month == request.Month, cancellationToken);
            if (exists) return Results.Conflict(new { error = "A configuration already exists for this seller and month." });
            var configuration = SellerClosingConfiguration.Create(request.Seller, request.Year, request.Month, request.BaseSalary, request.CommissionPercent, request.PppMaximumAward);
            dbContext.SellerClosingConfigurations.Add(configuration);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Created($"{prefix}/closing-configurations/{configuration.Id}", new { configuration.Id });
        }).RequireAuthorization(AuthorizationPolicies.AdministratorOnly);
        endpoints.MapGet($"{prefix}/closings", async (string seller, string month, ClaimsPrincipal user, ISellerClosingQueryService service, CancellationToken cancellationToken) =>
        {
            if (!user.IsInRole("Administrador") && !user.IsInRole("Gestor"))
            {
                var assignedSeller = user.FindFirstValue("seller");
                if (string.IsNullOrWhiteSpace(assignedSeller) ||
                    SellerAliasCatalog.ResolveImportedName(assignedSeller) != SellerAliasCatalog.ResolveImportedName(seller))
                    return Results.Forbid();
            }
            if (!DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", out var period)) return Results.BadRequest(new { error = "month must use yyyy-MM." });
            var result = await service.GetAsync(seller, period.Year, period.Month, cancellationToken);
            if (result is not null) return Results.Ok(result);
            var status = await service.GetConfigurationStatusAsync(seller, period.Year, period.Month, cancellationToken);
            return Results.NotFound(new { error = status.ErrorMessage });
        }).RequireAuthorization(AuthorizationPolicies.SellerScope);
        endpoints.MapGet($"{prefix}/closings/payroll", (string? month, string? coverageSeller,
            IPayrollClosingQueryService service, CancellationToken cancellationToken) =>
            GetPayrollAsync(month, coverageSeller, service, false, cancellationToken))
            .RequireAuthorization(AuthorizationPolicies.ManagerOrAdministrator);
        endpoints.MapGet($"{prefix}/closings/payroll/export", (string? month, string? coverageSeller,
            IPayrollClosingQueryService service, CancellationToken cancellationToken) =>
            GetPayrollAsync(month, coverageSeller, service, true, cancellationToken))
            .RequireAuthorization(AuthorizationPolicies.ManagerOrAdministrator);
        return endpoints;
    }

    private static async Task<IResult> GetPayrollAsync(string? month, string? coverageSeller,
        IPayrollClosingQueryService service, bool export, CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var period))
            return Results.BadRequest(new { error = "month deve usar o formato yyyy-MM." });

        var coverage = PayrollCatalog.CanonicalName(coverageSeller ?? PayrollCatalog.DefaultCoverage);
        if (!PayrollCatalog.StandardSellers.Contains(coverage, StringComparer.Ordinal))
            return Results.BadRequest(new { error = "coverageSeller deve ser um dos seis vendedores disponíveis para cobertura." });

        var closing = await service.GetPayrollAsync(coverage, period.Year, period.Month, cancellationToken);
        if (closing is null)
            return Results.NotFound(new { error = "Fechamento RH indisponível: faltam configurações salariais ou importações obrigatórias para o período selecionado." });

        return export
            ? Results.File(PayrollExcelExporter.Export(closing),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"fechamento-rh-{period.ToString("yyyy-MM", CultureInfo.InvariantCulture)}.xlsx")
            : Results.Ok(closing);
    }

    public sealed record ClosingConfigurationRequest(string Seller, int Year, int Month, decimal BaseSalary, decimal CommissionPercent, decimal PppMaximumAward);
}
