using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OroBI.Application.Closings;
using OroBI.Domain.Closings;
using OroBI.Domain.Commercial;
using OroBI.Infrastructure.Persistence;
using OroBI.Infrastructure.Portal;

namespace OroBI.Infrastructure.Tests.Portal;

public sealed class PortalClosingTests
{
    [Fact]
    public async Task Approved_month_remains_frozen_when_live_calculation_changes()
    {
        await using var db = Database();
        var calculator = new MutableClosingQuery();
        var service = new PortalClosingService(db, calculator);
        var seller = Guid.NewGuid();

        Assert.Equal("EmApuracao", (await service.GetAsync(seller, "ANA", 2026, 8, default))!.Status);
        await service.ReviewAsync(seller, "ANA", 2026, 8, "admin", default);
        await service.ApproveAsync(seller, "ANA", 2026, 8, "admin", default);
        calculator.Revenue = 9000m;

        var result = await service.GetAsync(seller, "ANA", 2026, 8, default);
        Assert.NotNull(result);
        Assert.Equal("Aprovado", result.Status);
        Assert.False(result.IsEstimated);
        Assert.Equal(1000m, result.Revenue);
        Assert.Equal(10m, result.Commission);
        Assert.Equal(100m, result.TotalAwards);
        Assert.NotNull(result.ApprovedAtUtc);
        Assert.Equal(1, await db.Set<ClosingSnapshot>().CountAsync());
    }

    [Fact]
    public async Task Cannot_approve_without_review_or_change_an_approved_month()
    {
        await using var db = Database();
        var service = new PortalClosingService(db, new MutableClosingQuery());
        var seller = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(seller, "ANA", 2026, 8, "admin", default));
        await service.ReviewAsync(seller, "ANA", 2026, 8, "admin", default);
        await service.ApproveAsync(seller, "ANA", 2026, 8, "admin", default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReviewAsync(seller, "ANA", 2026, 8, "admin", default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(seller, "ANA", 2026, 8, "admin", default));
    }

    [Fact]
    public async Task Personal_projection_omits_salary_documents_and_team_awards()
    {
        await using var db = Database();
        var service = new PortalClosingService(db, new MutableClosingQuery());
        var result = await service.GetAsync(Guid.NewGuid(), "ANA", 2026, 8, default);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("baseSalary", json);
        Assert.DoesNotContain("Supervisor", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SECRET COLLEAGUE", json);
        Assert.DoesNotContain("documents", json);
        Assert.DoesNotContain("900000", json);
        Assert.Equal(110m, result!.CommissionAndAwards);
    }

    [Fact]
    public async Task Special_closing_exposes_own_compensation_without_company_or_team_sales()
    {
        await using var db = Database();
        var service = new PortalClosingService(db, new MutableClosingQuery { Scope = "company-excluding-bauducco" });
        var result = await service.GetAsync(Guid.NewGuid(), "VALDIR ZACARIAS", 2026, 8, default);
        Assert.NotNull(result);
        Assert.Null(result.Revenue);
        Assert.Null(result.CommissionableRevenue);
        Assert.Null(result.TradeValue);
        Assert.Null(result.TradePercent);
        Assert.Null(result.CommissionPercent);
        Assert.Equal(10m, result.Commission);
    }

    [Fact]
    public async Task Snapshot_for_another_seller_or_month_is_never_used()
    {
        await using var db = Database();
        var calculator = new MutableClosingQuery();
        var service = new PortalClosingService(db, calculator);
        var seller = Guid.NewGuid();
        await service.ReviewAsync(seller, "ANA", 2026, 8, "admin", default);
        await service.ApproveAsync(seller, "ANA", 2026, 8, "admin", default);
        calculator.Revenue = 3000m;
        Assert.Equal("EmApuracao", (await service.GetAsync(Guid.NewGuid(), "BRUNO", 2026, 8, default))!.Status);
        Assert.Equal(3000m, (await service.GetAsync(seller, "ANA", 2026, 9, default))!.Revenue);
    }

    [Fact]
    public async Task Missing_calculation_or_empty_month_cannot_be_approved()
    {
        await using var db = Database();
        var calculator = new MutableClosingQuery { Available = false };
        var service = new PortalClosingService(db, calculator);
        var seller = Guid.NewGuid();
        Assert.Null(await service.GetAsync(seller, "ANA", 2026, 8, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReviewAsync(seller, "ANA", 2026, 8, "admin", default));
        calculator.Available = true;
        calculator.HasMovements = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReviewAsync(seller, "ANA", 2026, 8, "admin", default));
    }

    [Fact]
    public async Task History_includes_only_own_months_and_keeps_approved_snapshot_after_source_removal()
    {
        await using var db = Database();
        var service = new PortalClosingService(db, new MutableClosingQuery());
        var seller = Guid.NewGuid();
        await service.ReviewAsync(seller, "ANA", 2026, 8, "admin", default);
        await service.ApproveAsync(seller, "ANA", 2026, 8, "admin", default);
        db.AddRange(
            CommercialMovement.Create(Guid.NewGuid(), new DateOnly(2026, 7, 1), "ANA", "VENDA", 10m, 1m),
            CommercialMovement.Create(Guid.NewGuid(), new DateOnly(2026, 6, 1), "BRUNO", "VENDA", 999m, 1m));
        await db.SaveChangesAsync();
        var history = await service.GetHistoryAsync(seller, "ANA", default);
        Assert.Equal(new[] { "2026-08", "2026-07" }, history.Select(item => item.Month));
        Assert.Equal("Aprovado", history[0].Status);
    }

    private static OroBiDbContext Database() => new(new DbContextOptionsBuilder<OroBiDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class MutableClosingQuery : ISellerClosingQueryService
    {
        public decimal Revenue { get; set; } = 1000m;
        public bool Available { get; set; } = true;
        public bool HasMovements { get; set; } = true;
        public string Scope { get; set; } = "seller";
        public Task<SellerClosingSummary?> GetAsync(string seller, int year, int month, CancellationToken cancellationToken) =>
            Task.FromResult<SellerClosingSummary?>(!Available ? null : new SellerClosingSummary(new(80m, 80m), 10m, 5m, 5m,
                new(Revenue / 100m, 900000m + Revenue / 100m), 100m)
            {
                CommissionPercent = 1m,
                Monthly = new(Scope, Revenue, Revenue, 20m, 2m, HasMovements ? 2 : 0, 1,
                    [new("NF-SECRET", new(year, month, 1), "SECRET COLLEAGUE", "C1", "SECRET CLIENT", "VENDA", Revenue)]),
                Supervisor = new(1m, 2m, 3m, [], [new("SECRET COLLEAGUE", true, new("x", "x", 1m, 0m, 0m), 900000m, 1m)], 1m, 1m)
            });
        public Task<ClosingConfigurationStatus> GetConfigurationStatusAsync(string seller, int year, int month, CancellationToken cancellationToken) =>
            Task.FromResult(new ClosingConfigurationStatus(Available, Available, Available, Available));
    }
}
