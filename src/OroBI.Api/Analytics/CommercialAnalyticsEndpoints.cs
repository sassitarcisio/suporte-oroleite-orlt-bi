using OroBI.Application.Analytics;
using OroBI.Api.Auth;

namespace OroBI.Api.Analytics;

public static class CommercialAnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapCommercialAnalyticsEndpoints(this IEndpointRouteBuilder endpoints, string prefix = "/api")
    {
        endpoints.MapGet($"{prefix}/trades", async ([AsParameters] DashboardQueryParameters query, ICommercialAnalyticsQueryService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetTradesAsync(query.ToCommercialFilter(), cancellationToken))).RequireAuthorization(AuthorizationPolicies.ManagerOrAdministrator);
        endpoints.MapGet($"{prefix}/trade-analysis", async ([AsParameters] DashboardQueryParameters query, ICommercialAnalyticsQueryService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetTradeAnalysisAsync(query.ToCommercialFilter(), cancellationToken))).RequireAuthorization(AuthorizationPolicies.ManagerOrAdministrator);
        endpoints.MapGet($"{prefix}/sales-trades", async ([AsParameters] DashboardQueryParameters query, ICommercialAnalyticsQueryService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetSalesTradesAsync(query.ToCommercialFilter(), cancellationToken))).RequireAuthorization(AuthorizationPolicies.ManagerOrAdministrator);
        endpoints.MapGet($"{prefix}/margins", async ([AsParameters] DashboardQueryParameters query, ICommercialAnalyticsQueryService service, CancellationToken cancellationToken) =>
        {
            var report = await service.GetMarginsAsync(query.ToCommercialFilter(), cancellationToken);
            return Results.Ok(new { report.Revenue, report.Cost, report.GrossProfit, report.MarginPercent });
        }).RequireAuthorization(AuthorizationPolicies.ManagerOrAdministrator);
        endpoints.MapGet($"{prefix}/margins/details", async ([AsParameters] DashboardQueryParameters query, ICommercialAnalyticsQueryService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetMarginsAsync(query.ToCommercialFilter(), cancellationToken))).RequireAuthorization(AuthorizationPolicies.ManagerOrAdministrator);
        endpoints.MapGet($"{prefix}/net-margin", async ([AsParameters] DashboardQueryParameters query, ICommercialAnalyticsQueryService service, CancellationToken cancellationToken) =>
        {
            var report = await service.GetNetMarginAsync(query.ToCommercialFilter(), cancellationToken);
            return Results.Ok(new
            {
                report.GrossSales, report.Returns, report.NetSales, report.NetCost, report.TradeLosses,
                report.BoletoDiscounts, report.LiquidProfit, report.LiquidMarginPercent, report.ProductCount
            });
        }).RequireAuthorization(AuthorizationPolicies.ManagerOrAdministrator);
        endpoints.MapGet($"{prefix}/net-margin/details", async ([AsParameters] DashboardQueryParameters query, ICommercialAnalyticsQueryService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetNetMarginAsync(query.ToCommercialFilter(), cancellationToken))).RequireAuthorization(AuthorizationPolicies.ManagerOrAdministrator);
        return endpoints;
    }
}
