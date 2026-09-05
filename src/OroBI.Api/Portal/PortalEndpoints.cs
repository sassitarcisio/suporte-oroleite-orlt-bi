using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OroBI.Application.Identity;
using OroBI.Application.Portal;
using OroBI.Domain.Sellers;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Api.Portal;

public static class PortalEndpoints
{
    public static IEndpointRouteBuilder MapPortalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var me = endpoints.MapGroup("/api/v1/me").RequireAuthorization();
        me.MapGet("/{resource}", (string resource, HttpContext http, IDataAccessScope scope, IPortalQueryService queries,
            IPortalClosingService closings, CancellationToken ct) => ReadAsync(resource, null, null, http, scope, queries, closings, ct));
        me.MapGet("/customers/{customerCode}", (string customerCode, HttpContext http, IDataAccessScope scope,
            IPortalQueryService queries, IPortalClosingService closings, CancellationToken ct) =>
            ReadAsync("customer", null, customerCode, http, scope, queries, closings, ct));
        me.MapGet("/closings/history", (HttpContext http, IDataAccessScope scope, IPortalQueryService queries,
            IPortalClosingService closings, CancellationToken ct) => ReadAsync("history", null, null, http, scope, queries, closings, ct));

        var management = endpoints.MapGroup("/api/v1/management/sellers").RequireAuthorization();
        management.MapGet("", ListSellersAsync);
        management.MapGet("/{sellerId:guid}/{resource}", (Guid sellerId, string resource, HttpContext http, IDataAccessScope scope,
            IPortalQueryService queries, IPortalClosingService closings, CancellationToken ct) =>
            ReadAsync(resource, sellerId, null, http, scope, queries, closings, ct));
        management.MapGet("/{sellerId:guid}/customers/{customerCode}", (Guid sellerId, string customerCode, HttpContext http,
            IDataAccessScope scope, IPortalQueryService queries, IPortalClosingService closings, CancellationToken ct) =>
            ReadAsync("customer", sellerId, customerCode, http, scope, queries, closings, ct));
        management.MapGet("/{sellerId:guid}/closings/history", (Guid sellerId, HttpContext http, IDataAccessScope scope,
            IPortalQueryService queries, IPortalClosingService closings, CancellationToken ct) =>
            ReadAsync("history", sellerId, null, http, scope, queries, closings, ct));
        management.MapPost("/{sellerId:guid}/closings/{action}", ChangeClosingAsync)
            .RequireAuthorization("AdministratorOnly");
        return endpoints;
    }

    private static bool IsManagement(ClaimsPrincipal user) =>
        new[] { "Administrador", "Diretoria", "Gestor", "Gerente" }.Any(user.IsInRole);

    private static async Task<IResult> ListSellersAsync(HttpContext http, OroBiDbContext db, IDataAccessScope scope, CancellationToken ct)
    {
        if (!IsManagement(http.User)) return Results.Forbid();
        var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var sellers = db.Sellers.AsNoTracking().AsQueryable();
        if (!http.User.IsInRole("Administrador") && !http.User.IsInRole("Diretoria"))
            sellers = sellers.Where(s => s.IsActive && db.UserSellerAccesses.Any(a => a.SellerId == s.Id && a.UserId == userId && a.IsActive));
        var ids = await sellers.OrderBy(s => s.Name).Select(s => s.Id).Take(500).ToArrayAsync(ct);
        var result = new List<SellerAccess>();
        foreach (var id in ids)
            if (await scope.ResolveAsync(http.User, id, ct) is { } access) result.Add(access);
        return Results.Ok(result);
    }

    private static async Task<IResult> ReadAsync(string resource, Guid? sellerId, string? customerCode, HttpContext http,
        IDataAccessScope scope, IPortalQueryService queries, IPortalClosingService closings, CancellationToken ct)
    {
        if (sellerId is not null && !IsManagement(http.User)) return Results.Forbid();
        // Personal requests cannot select an identity by query string, even if the supplied name looks valid.
        if (http.Request.Query.Keys.Any(key => key.Equals("seller", StringComparison.OrdinalIgnoreCase) || key.Equals("sellerId", StringComparison.OrdinalIgnoreCase)))
            return Results.Forbid();
        var access = await scope.ResolveAsync(http.User, sellerId, ct);
        if (access is null) return Results.Forbid();
        if (!PortalRequest.TryRead(http.Request, out var request)) return Results.BadRequest(new { error = "Período ou paginação inválidos. Use datas yyyy-MM-dd e mês yyyy-MM." });
        var p = access.Permissions;
        if (!CanRead(resource, p)) return Results.Forbid();
        var name = access.ImportedName;
        switch (resource)
        {
            case "dashboard":
                var dashboard = await queries.GetDashboardAsync(name, request.Filter, ct);
                return Results.Ok(p.CanViewCustomers ? dashboard : dashboard with
                {
                    Period = dashboard.Period with { CustomerCount = null },
                    Month = dashboard.Month with { CustomerCount = null },
                    Today = dashboard.Today with { CustomerCount = null }
                });
            case "sales": return Results.Ok(await queries.GetSalesAsync(name, request.Filter, request.Page, request.PageSize, ct));
            case "customers": return Results.Ok(await queries.GetCustomersAsync(name, request.Filter, ct));
            case "customer":
                var customer = await queries.GetCustomerAsync(name, customerCode!, request.Filter, ct);
                return customer is null ? Results.NotFound(new { error = "Cliente não encontrado neste período." }) : Results.Ok(customer);
            case "products":
            case "brands":
                var ranking = resource == "products" ? await queries.GetProductsAsync(name, request.Filter, ct) : await queries.GetBrandsAsync(name, request.Filter, ct);
                return Results.Ok(p.CanViewCustomers ? ranking : ranking with { Items = ranking.Items.Select(item => item with { CustomerCount = null }).ToArray() });
            case "goals":
                var goals = await queries.GetGoalsAsync(name, request.Year, request.Month, ct);
                return Results.Ok(goals with { Items = goals.Items.Where(g => p.CanViewRevenue || g.Type != "FATURAMENTO")
                    .Select(g => p.CanViewPrize ? g : g with { MaximumPrize = null, CurrentPrize = null, NextTierPrize = null }).ToArray() });
            case "ppp":
                var ppp = await queries.GetPppAsync(name, request.Year, request.Month, ct);
                return Results.Ok(ppp with { Award = p.CanViewPrize ? ppp.Award : null,
                    Segments = p.CanViewCustomers ? ppp.Segments : ppp.Segments.Select(item => item with { CustomerCount = null }).ToArray() });
            case "trades": return Results.Ok(await queries.GetTradesAsync(name, request.Filter, ct));
            case "history": return Results.Ok(await closings.GetHistoryAsync(access.SellerId, name, ct));
            case "commission":
            case "closings":
                var closing = await closings.GetAsync(access.SellerId, name, request.Year, request.Month, ct);
                return closing is null ? Results.NotFound(new { error = "Fechamento indisponível: confira as importações e configurações do período." })
                    : Results.Ok(ApplyPermissions(closing, p));
            default: return Results.NotFound();
        }
    }

    private static bool CanRead(string resource, SellerPortalPermissions p) => resource switch
    {
        "dashboard" or "products" or "brands" => p.CanViewRevenue,
        "sales" or "customers" or "customer" => p.CanViewRevenue && p.CanViewCustomers,
        "goals" => p.CanViewGoals,
        "ppp" => p.CanViewPPP,
        "trades" => p.CanViewTrades && p.CanViewRevenue && p.CanViewCustomers,
        "commission" => p.CanViewCommission,
        "closings" or "history" => p.CanViewCommission || p.CanViewPrize,
        _ => true
    };

    internal static PortalClosing ApplyPermissions(PortalClosing value, SellerPortalPermissions p)
    {
        var allAwards = p.CanViewPrize && p.CanViewPPP && p.CanViewGoals && p.CanViewTrades;
        return value with
        {
            Revenue = p.CanViewRevenue ? value.Revenue : null,
            CommissionableRevenue = p.CanViewRevenue ? value.CommissionableRevenue : null,
            Commission = p.CanViewCommission ? value.Commission : null,
            CommissionPercent = p.CanViewCommission && p.CanViewRevenue ? value.CommissionPercent : null,
            PppPercent = p.CanViewPPP ? value.PppPercent : null,
            PppAward = p.CanViewPPP && p.CanViewPrize ? value.PppAward : null,
            RevenueAward = p.CanViewGoals && p.CanViewPrize ? value.RevenueAward : null,
            PositivityAward = p.CanViewGoals && p.CanViewPrize ? value.PositivityAward : null,
            TradeAward = p.CanViewTrades && p.CanViewPrize ? value.TradeAward : null,
            TotalAwards = allAwards ? value.TotalAwards : null,
            TradeValue = p.CanViewTrades ? value.TradeValue : null,
            TradePercent = p.CanViewTrades && p.CanViewRevenue ? value.TradePercent : null,
            CommissionAndAwards = allAwards && p.CanViewCommission ? value.CommissionAndAwards : null
        };
    }

    private static async Task<IResult> ChangeClosingAsync(Guid sellerId, string action, HttpContext http, IDataAccessScope scope,
        IPortalClosingService closings, OroBiDbContext db, CancellationToken ct)
    {
        if (action is not ("review" or "approve")) return Results.NotFound();
        var access = await scope.ResolveAsync(http.User, sellerId, ct);
        if (access is null) return Results.Forbid();
        if (!await db.Sellers.AnyAsync(item => item.Id == sellerId && item.IsActive, ct))
            return Results.Conflict(new { error = "O vendedor está desativado. Seu histórico permanece disponível para consulta." });
        if (!http.Request.Query.ContainsKey("month") || !PortalRequest.TryRead(http.Request, out var request))
            return Results.BadRequest(new { error = "Informe o mês no formato yyyy-MM." });
        var actor = http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        try
        {
            var result = action == "review"
                ? await closings.ReviewAsync(sellerId, access.ImportedName, request.Year, request.Month, actor, ct)
                : await closings.ApproveAsync(sellerId, access.ImportedName, request.Year, request.Month, actor, ct);
            return Results.Ok(result);
        }
        catch (InvalidOperationException error) { return Results.Conflict(new { error = error.Message }); }
        catch (DbUpdateException) { return Results.Conflict(new { error = "O fechamento foi atualizado por outra operação. Atualize a tela." }); }
    }
}
