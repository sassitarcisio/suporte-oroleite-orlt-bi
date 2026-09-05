using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OroBI.Application.Analytics;
using OroBI.Domain.Closings;
using OroBI.Domain.Commercial;
using OroBI.Domain.Goals;
using OroBI.Domain.Imports;
using OroBI.Domain.Ppp;
using OroBI.Domain.Synchronization;
using OroBI.Domain.Sellers;
using OroBI.Infrastructure.Closings;
using OroBI.Infrastructure.Persistence;
using OroBI.Infrastructure.Portal;

namespace OroBI.Infrastructure.Tests.Portal;

public sealed class PortalQueryServiceTests
{
    private static readonly CommercialFilter August = new(new(2026, 8, 1), new(2026, 8, 31));

    [Fact]
    public async Task Dashboard_scopes_before_aggregation_and_ignores_a_foreign_filter_seller()
    {
        await using var db = await Fixture();
        var result = await Service(db).GetDashboardAsync("ANA", August with { Seller = "BOB" }, default);
        Assert.Equal(1000m, result.Period.GrossSales);
        Assert.Equal(980m, result.Period.NetRevenue);
        Assert.Equal(2, result.Period.MovementCount);
        Assert.Equal(1, result.Period.CustomerCount);
        Assert.Equal(2, result.DailyTrend.Count);
        Assert.Equal("csv", result.Freshness.Source);
        Assert.Equal("import-started", result.Freshness.TimestampKind);
        var json = JsonSerializer.Serialize(result).ToLowerInvariant();
        Assert.DoesNotContain("bob", json);
        Assert.DoesNotContain("cost", json);
        Assert.DoesNotContain("salary", json);
        Assert.DoesNotContain("sellerresults", json);
    }

    [Fact]
    public async Task Sales_page_filters_counts_and_customer_history_never_cross_sellers()
    {
        await using var db = await Fixture();
        var service = Service(db);
        var page = await service.GetSalesAsync("ANA", August, 2, 1, default);
        Assert.Equal(2, page.TotalCount);
        Assert.Single(page.Items);
        Assert.Equal(1000m, page.Items[0].TotalValue);
        var filtered = await service.GetSalesAsync("ANA", August with { ProductContains = "cafe", Brand = "nestle" }, 1, 20, default);
        Assert.Equal(2, filtered.TotalCount);
        var customers = await service.GetCustomersAsync("ANA", August, default);
        Assert.True(customers.ObservedBuyersOnly);
        Assert.Single(customers.Items);
        Assert.Null(await service.GetCustomerAsync("ANA", "BOB-ONLY", August, default));
        var detail = await service.GetCustomerAsync("ANA", "SHARED", August, default);
        Assert.NotNull(detail);
        Assert.Equal(2, detail.Sales.Count);
        Assert.DoesNotContain(detail.Sales, item => item.TotalValue == 9000m);
        Assert.Equal(980m, Assert.Single((await service.GetProductsAsync("ANA", August, default)).Items).NetRevenue);
        Assert.Equal("NESTLE", Assert.Single((await service.GetBrandsAsync("ANA", August, default)).Items).Label);
        Assert.Equal(20m, (await service.GetTradesAsync("ANA", August, default)).PhysicalTrades);
    }

    [Theory]
    [InlineData(79, 0, 80, 1, 50)]
    [InlineData(80, 50, 90, 10, 75)]
    [InlineData(90, 75, 100, 10, 100)]
    [InlineData(100, 100, null, null, null)]
    [InlineData(110, 100, null, null, null)]
    public async Task Revenue_next_tier_uses_imported_goal_actual_and_official_prizes(int actual, int prize, int? nextPercent, int? missing, int? nextPrize)
    {
        await using var db = await Fixture(actual);
        var result = await Service(db).GetGoalsAsync("ANA", 2026, 8, default);
        var revenue = Assert.Single(result.Items, item => item.Type == "FATURAMENTO");
        Assert.Equal(actual, revenue.Actual);
        Assert.Equal(prize, revenue.CurrentPrize);
        Assert.Equal(nextPercent is null ? null : (decimal?)nextPercent, revenue.NextTierPercent);
        Assert.Equal(missing is null ? null : (decimal?)missing, revenue.AmountToNextTier);
        Assert.Equal(nextPrize is null ? null : (decimal?)nextPrize, revenue.NextTierPrize);
        var positivity = Assert.Single(result.Items, item => item.Type == "POSITIVACAO");
        Assert.Equal(actual >= 100 ? 50m : 0m, positivity.CurrentPrize);
        Assert.Equal(actual >= 100 ? null : (decimal?)100, positivity.NextTierPercent);
    }

    [Fact]
    public async Task Zero_target_and_missing_configuration_are_unavailable_without_invented_prizes()
    {
        await using var db = await Fixture(10, 0);
        var service = Service(db);
        var result = await service.GetGoalsAsync("ANA", 2026, 8, default);
        var revenue = Assert.Single(result.Items, item => item.Type == "FATURAMENTO");
        Assert.Null(revenue.AchievedPercent);
        Assert.Null(revenue.NextTierPercent);
        Assert.Null(revenue.AmountToNextTier);
        db.ImportedClosingDefaults.RemoveRange(db.ImportedClosingDefaults);
        await db.SaveChangesAsync();
        result = await service.GetGoalsAsync("ANA", 2026, 8, default);
        Assert.False(result.Available);
        Assert.All(result.Items, item => Assert.Null(item.CurrentPrize));
        var ppp = await service.GetPppAsync("ANA", 2026, 8, default);
        Assert.Null(ppp.Award);
        Assert.Single(ppp.Segments);
    }

    [Fact]
    public async Task Ppp_uses_own_segments_and_official_calculation()
    {
        await using var db = await Fixture();
        var result = await Service(db).GetPppAsync("ANA", 2026, 8, default);
        Assert.True(result.Available);
        Assert.Equal(50m, result.AchievementPercent);
        Assert.Equal(600m, result.Award);
        Assert.Equal("MERCEARIA", Assert.Single(result.Segments).Segment);
    }

    [Fact]
    public async Task Imported_alias_preserves_the_official_seller_ppp_configuration()
    {
        await using var db = await Fixture();
        db.CommercialMovements.RemoveRange(db.CommercialMovements);
        db.PppRecords.RemoveRange(db.PppRecords);
        db.AddRange(SellerClosingConfiguration.Create("MARCIO FERNANDES", 2026, 8, 1951, 1, 2000),
            PppRecord.Create(Guid.NewGuid(), 2026, 8, "VENDEDOR: MARCIO FERNANDES", "SEGMENTO", 10, 2, 10));
        await db.SaveChangesAsync();
        var result = await Service(db).GetPppAsync("VENDEDOR: MARCIO FERNANDES", 2026, 8, default);
        Assert.Equal(1000m, result.Award);
    }

    [Theory]
    [InlineData("SUPERVISOR: DEIVID MANNES", "DEIVID MANNES", "supervisor-union")]
    [InlineData("VENDEDOR: VALDIR ZACARIAS", "VALDIR ZACARIAS", "company-excluding-bauducco")]
    public async Task Imported_alias_uses_the_same_special_closing_as_canonical_name(string imported, string canonical, string expectedScope)
    {
        await using var db = await Fixture();
        db.Add(SellerClosingConfiguration.Create(canonical, 2026, 8, 3000, 1, 1200));
        await db.SaveChangesAsync();
        var service = new SellerClosingQueryService(db);
        var expected = await service.GetAsync(canonical, 2026, 8, default);
        var actual = await service.GetAsync(imported, 2026, 8, default);
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expectedScope, actual.Monthly.Scope);
        Assert.Equal(expected.Compensation, actual.Compensation);
        Assert.Equal(expected.TotalAwards, actual.TotalAwards);
    }

    [Fact]
    public async Task Default_month_and_today_follow_Brazil_date_at_utc_month_boundary()
    {
        await using var db = await Fixture();
        var batch = Batch(ImportFileType.Power);
        db.AddRange(batch, Movement(batch.Id, "ANA", "SHARED", 50, 31),
            CommercialMovement.Create(batch.Id, new(2026, 9, 1), "ANA", "VENDA", 700, 1));
        await db.SaveChangesAsync();
        var service = new PortalQueryService(db, new SellerClosingQueryService(db), new FixedClock(new(2026, 9, 1, 1, 0, 0, TimeSpan.Zero)));
        var result = await service.GetDashboardAsync("ANA", new(), default);
        Assert.Equal(new DateOnly(2026, 8, 31), result.ReferenceDate);
        Assert.Equal(new DateOnly(2026, 8, 1), result.StartDate);
        Assert.Equal(1030m, result.Period.NetRevenue);
        Assert.Equal(1030m, result.Month.NetRevenue);
        Assert.Equal(50m, result.Today.NetRevenue);
    }

    [Fact]
    public async Task Freshness_requires_a_completed_firebird_run_and_does_not_invent_a_timestamp()
    {
        await using var db = await Fixture();
        var service = Service(db);
        var pending = SynchronizationRun.Start("FIREBIRD");
        var other = SynchronizationRun.Start("OTHER");
        db.AddRange(pending, other);
        db.Entry(other).Property(item => item.Status).CurrentValue = SynchronizationRunStatus.Completed;
        db.Entry(other).Property(item => item.CompletedAtUtc).CurrentValue = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        Assert.Equal("csv", (await service.GetDataFreshnessAsync(default)).Source);
        db.Entry(pending).Property(item => item.Status).CurrentValue = SynchronizationRunStatus.Completed;
        var finished = DateTimeOffset.UtcNow;
        db.Entry(pending).Property(item => item.CompletedAtUtc).CurrentValue = finished;
        await db.SaveChangesAsync();
        var freshness = await service.GetDataFreshnessAsync(default);
        Assert.Equal("firebird", freshness.Source);
        Assert.Equal("sync-completed", freshness.TimestampKind);
        Assert.Equal(finished, freshness.UpdatedAtUtc);
        db.SynchronizationRuns.RemoveRange(db.SynchronizationRuns);
        db.ImportBatches.RemoveRange(db.ImportBatches);
        await db.SaveChangesAsync();
        freshness = await service.GetDataFreshnessAsync(default);
        Assert.Equal("unavailable", freshness.Source);
        Assert.Null(freshness.UpdatedAtUtc);
    }

    [Fact]
    public async Task Customer_payload_is_bounded_and_return_only_customers_are_not_invented_buyers()
    {
        await using var db = await Fixture();
        var batch = Batch(ImportFileType.Power);
        db.Add(batch);
        for (var index = 0; index < 205; index++) db.Add(Movement(batch.Id, "ANA", $"CUSTOMER-{index:D3}", 100, 3));
        db.Add(Movement(batch.Id, "ANA", "RETURN-ONLY", -20, 3, "TROCA"));
        await db.SaveChangesAsync();
        var result = await Service(db).GetCustomersAsync("ANA", August, default);
        Assert.Equal(206, result.TotalCount);
        Assert.Equal(200, result.Items.Count);
        Assert.True(result.HasMore);
        Assert.DoesNotContain(result.Items, item => item.CustomerCode == "RETURN-ONLY");
    }

    [Fact]
    public async Task Service_rejects_empty_scope_invalid_dates_and_unbounded_pages()
    {
        await using var db = await Fixture();
        var service = Service(db);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetDashboardAsync("", August, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetDashboardAsync("ANA", August with { StartDate = new(2026, 9, 1) }, default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetSalesAsync("ANA", August, 0, 20, default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetSalesAsync("ANA", August, 1, 101, default));
    }

    [Fact]
    public async Task Dashboard_and_customer_ticket_use_net_revenue_per_document_and_purchase_quantity()
    {
        await using var db = await Fixture();
        var service = Service(db);
        var dashboard = await service.GetDashboardAsync("ANA", August, default);
        Assert.Equal(490m, dashboard.Period.AverageTicket);
        var customer = Assert.Single((await service.GetCustomersAsync("ANA", August, default)).Items);
        Assert.Equal(490m, customer.AverageTicket);
        Assert.Equal(1m, customer.PurchasedQuantity);
        var detail = await service.GetCustomerAsync("ANA", "SHARED", August, default);
        Assert.NotNull(detail);
        Assert.Equal(customer.AverageTicket, detail.Customer.AverageTicket);
        Assert.Equal(customer.PurchasedQuantity, detail.Customer.PurchasedQuantity);
    }

    [Fact]
    public async Task Ticket_is_unavailable_without_a_document_denominator()
    {
        await using var db = await Fixture();
        foreach (var movement in db.CommercialMovements)
            db.Entry(movement).Property(item => item.DocumentNumber).CurrentValue = string.Empty;
        await db.SaveChangesAsync();
        var service = Service(db);
        var dashboard = await service.GetDashboardAsync("ANA", August, default);
        Assert.Null(dashboard.Period.AverageTicket);
        Assert.Null(Assert.Single((await service.GetCustomersAsync("ANA", August, default)).Items).AverageTicket);
    }

    [Fact]
    public async Task Customer_filter_matches_code_or_name_inside_authorized_seller_and_other_filters()
    {
        await using var db = await Fixture();
        foreach (var movement in db.CommercialMovements)
            db.Entry(movement).Property(item => item.CustomerName).CurrentValue = "LOJA CENTRO";
        await db.SaveChangesAsync();
        var service = Service(db);
        var byCode = await service.GetSalesAsync("ANA", August with { CustomerContains = "shared", City = "cidade" }, 1, 20, default);
        Assert.Equal(2, byCode.TotalCount);
        Assert.Equal(980m, byCode.Items.Sum(item => item.TotalValue));
        var byName = await service.GetSalesAsync("ANA", August with { CustomerContains = "centro" }, 1, 20, default);
        Assert.Equal(2, byName.TotalCount);
        var foreign = await service.GetSalesAsync("ANA", August with { CustomerContains = "BOB-ONLY" }, 1, 20, default);
        Assert.Empty(foreign.Items);
        var wrongCity = await service.GetSalesAsync("ANA", August with { CustomerContains = "shared", City = "OUTRA" }, 1, 20, default);
        Assert.Empty(wrongCity.Items);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Approved_goals_and_ppp_use_only_snapshot_values_after_live_changes_or_removal(bool removeSource)
    {
        await using var db = await Fixture();
        var seller = new Seller { Name = "Ana", ImportedName = "ANA" };
        db.Sellers.Add(seller);
        await db.SaveChangesAsync();
        var calculator = new SellerClosingQueryService(db);
        var closing = new PortalClosingService(db, calculator);
        await closing.ReviewAsync(seller.Id, "ANA", 2026, 8, "admin", default);
        await closing.ApproveAsync(seller.Id, "ANA", 2026, 8, "admin", default);
        if (removeSource)
        {
            db.GoalRecords.RemoveRange(db.GoalRecords);
            db.PppRecords.RemoveRange(db.PppRecords);
            db.ImportedClosingDefaults.RemoveRange(db.ImportedClosingDefaults);
        }
        else
        {
            foreach (var goal in db.GoalRecords.Where(item => item.Seller == "ANA"))
                db.Entry(goal).Property(item => item.Achieved).CurrentValue = 20m;
            foreach (var segment in db.PppRecords.Where(item => item.Seller == "ANA"))
                db.Entry(segment).Property(item => item.GroupsPlaced).CurrentValue = 2;
        }
        await db.SaveChangesAsync();
        var service = Service(db);
        var goals = await service.GetGoalsAsync("ANA", 2026, 8, default);
        var revenue = Assert.Single(goals.Items, item => item.Type == "FATURAMENTO");
        Assert.Equal(80m, revenue.Actual);
        Assert.Equal(50m, revenue.CurrentPrize);
        Assert.Equal(90m, revenue.NextTierPercent);
        Assert.Equal(10m, revenue.AmountToNextTier);
        Assert.True(goals.Available);
        var goalJson = System.Text.Json.JsonSerializer.SerializeToElement(goals);
        Assert.True(goalJson.TryGetProperty("IsApproved", out var goalStatus) && goalStatus.GetBoolean());
        var ppp = await service.GetPppAsync("ANA", 2026, 8, default);
        Assert.Equal(50m, ppp.AchievementPercent);
        Assert.Equal(600m, ppp.Award);
        Assert.Equal(10, Assert.Single(ppp.Segments).GroupsPlaced);
        Assert.True(ppp.Available);
        var pppJson = System.Text.Json.JsonSerializer.SerializeToElement(ppp);
        Assert.True(pppJson.TryGetProperty("IsApproved", out var pppStatus) && pppStatus.GetBoolean());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Closing_review_rejects_missing_prize_values_for_an_imported_goal(bool unrelatedValuesExist)
    {
        await using var db = await Fixture();
        db.GoalValueRecords.RemoveRange(db.GoalValueRecords);
        if (unrelatedValuesExist)
        {
            var defaults = await db.ImportedClosingDefaults.SingleAsync();
            db.Add(GoalValueRecord.Create(defaults.ImportBatchId, "OUTRA MARCA", 100, 50, 25, 2));
        }
        var seller = new Seller { Name = "Ana", ImportedName = "ANA" };
        db.Add(seller);
        await db.SaveChangesAsync();
        var service = new PortalClosingService(db, new SellerClosingQueryService(db));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReviewAsync(seller.Id, "ANA", 2026, 8, "admin", default));
        Assert.Empty(await db.ClosingSnapshots.ToListAsync());
    }

    [Fact]
    public async Task Closing_approval_rechecks_prize_values_removed_after_review()
    {
        await using var db = await Fixture();
        var seller = new Seller { Name = "Ana", ImportedName = "ANA" };
        db.Add(seller);
        await db.SaveChangesAsync();
        var service = new PortalClosingService(db, new SellerClosingQueryService(db));
        await service.ReviewAsync(seller.Id, "ANA", 2026, 8, "admin", default);
        db.GoalValueRecords.RemoveRange(db.GoalValueRecords);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(seller.Id, "ANA", 2026, 8, "admin", default));
        var snapshot = await db.ClosingSnapshots.SingleAsync();
        Assert.Equal(ClosingApprovalStatus.EmConferencia, snapshot.Status);
        Assert.Null(snapshot.SnapshotJson);
    }

    [Theory]
    [InlineData("ANA")]
    [InlineData("VALDIR ZACARIAS")]
    [InlineData("DEIVID MANNES")]
    public async Task Closing_without_personal_goals_or_with_special_scope_can_still_be_approved(string name)
    {
        await using var db = await Fixture();
        db.GoalValueRecords.RemoveRange(db.GoalValueRecords);
        db.GoalRecords.RemoveRange(db.GoalRecords.Where(item => item.Seller == "ANA"));
        var seller = new Seller { Name = name, ImportedName = SellerAliasCatalog.ResolveImportedName(name) };
        db.Add(seller);
        if (name != "ANA")
        {
            db.Add(SellerClosingConfiguration.Create(name, 2026, 8, 3000, 1, 1200));
            db.Add(GoalRecord.Create(Guid.NewGuid(), seller.ImportedName, 8, 2026, "FATURAMENTO", "Marca SEM VALOR / Valor", 100, 80));
            db.Add(Movement(Guid.NewGuid(), seller.ImportedName, "SPECIAL", 100, 3));
        }
        await db.SaveChangesAsync();
        var service = new PortalClosingService(db, new SellerClosingQueryService(db));

        await service.ReviewAsync(seller.Id, seller.ImportedName, 2026, 8, "admin", default);
        var approved = await service.ApproveAsync(seller.Id, seller.ImportedName, 2026, 8, "admin", default);
        Assert.Equal("Aprovado", approved.Status);
    }

    [Fact]
    public async Task Ticket_counts_reused_numbers_as_distinct_documents_but_keeps_multiple_lines_together()
    {
        await using var db = await Fixture();
        db.CommercialMovements.RemoveRange(db.CommercialMovements);
        var batch = Batch(ImportFileType.Power);
        db.Add(batch);
        foreach (var (day, value) in new[] { (1, 40m), (1, 60m), (2, 200m) })
        {
            var movement = Movement(batch.Id, "ANA", "SHARED", value, day);
            db.Entry(movement).Property(item => item.DocumentNumber).CurrentValue = "123";
            db.Add(movement);
        }
        await db.SaveChangesAsync();
        var service = Service(db);

        var dashboard = await service.GetDashboardAsync("ANA", August, default);
        Assert.Equal(2, dashboard.Period.DocumentCount);
        Assert.Equal(150m, dashboard.Period.AverageTicket);
        var customer = Assert.Single((await service.GetCustomersAsync("ANA", August, default)).Items);
        Assert.Equal(150m, customer.AverageTicket);
        var detail = await service.GetCustomerAsync("ANA", "SHARED", August, default);
        Assert.NotNull(detail);
        Assert.Equal(150m, detail.Customer.AverageTicket);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task Product_ranking_preserves_codes_and_separates_identical_names_within_seller_scope()
    {
        await using var db = new OroBiDbContext(new DbContextOptionsBuilder<OroBiDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var batch = Batch(ImportFileType.Power);
        db.Add(batch);
        foreach (var (seller, code, name, amount, day) in new[]
        {
            ("ANA", "00123", "Leite integral", 100m, 1),
            ("ANA", "00123", "Leite integral atualizado", 50m, 2),
            ("ANA", "00456", "Leite integral", 200m, 1),
            ("ANA", "", "Leite integral", 25m, 1),
            ("BOB", "PRIVATE-CODE", "Leite integral", 9000m, 1)
        })
            db.Add(CommercialMovement.CreateFromImport(batch.Id, new(2026, 8, day), seller, "NESTLE", "REDE", "VENDA", "CIDADE", "Cliente", name, amount, 1, 0, "SHARED", $"{seller}-{day}", code));
        await db.SaveChangesAsync();

        var products = await Service(db).GetProductsAsync("ANA", August, default);
        Assert.Equal(3, products.TotalCount);
        var updated = Assert.Single(products.Items, item => item.ProductCode == "00123");
        Assert.Equal("Leite integral atualizado", updated.Label);
        Assert.Equal(150m, updated.NetRevenue);
        Assert.Equal(2m, updated.Quantity);
        Assert.Equal(200m, Assert.Single(products.Items, item => item.ProductCode == "00456").NetRevenue);
        Assert.Equal(25m, Assert.Single(products.Items, item => item.ProductCode is null).NetRevenue);
        Assert.Equal(375m, products.Items.Sum(item => item.NetRevenue));
        Assert.DoesNotContain(products.Items, item => item.ProductCode == "PRIVATE-CODE");
        var brand = Assert.Single((await Service(db).GetBrandsAsync("ANA", August, default)).Items);
        Assert.Equal("NESTLE", brand.Label);
        Assert.Null(brand.ProductCode);
        Assert.Equal(375m, brand.NetRevenue);
    }

    private static PortalQueryService Service(OroBiDbContext db) => new(db, new SellerClosingQueryService(db));

    private static async Task<OroBiDbContext> Fixture(decimal actual = 80, decimal target = 100)
    {
        var db = new OroBiDbContext(new DbContextOptionsBuilder<OroBiDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var old = Batch(ImportFileType.Power, "same-content");
        var current = Batch(ImportFileType.Power, "same-content");
        db.Entry(old).Property(item => item.StartedAtUtc).CurrentValue = DateTimeOffset.UtcNow.AddDays(-1);
        var goals = Batch(ImportFileType.Goals);
        var values = Batch(ImportFileType.GoalValues);
        var ppp = Batch(ImportFileType.Ppp);
        db.AddRange(old, current, goals, values, ppp);
        db.AddRange(
            Movement(old.Id, "ANA", "SHARED", 1000, 1),
            Movement(current.Id, "ANA", "SHARED", 1000, 1),
            Movement(current.Id, "ANA", "SHARED", -20, 2, "TROCA"),
            Movement(current.Id, "BOB", "SHARED", 9000, 1),
            Movement(current.Id, "BOB", "BOB-ONLY", 7000, 2),
            ImportedClosingDefaults.Create(values.Id, 1951, 1, 1200, new Dictionary<string, decimal>()),
            GoalValueRecord.Create(values.Id, "NESTLE", 100, 50, 25, 2),
            GoalRecord.Create(goals.Id, "ANA", 8, 2026, "FATURAMENTO", "Marca NESTLE / Valor", target, actual),
            GoalRecord.Create(goals.Id, "ANA", 8, 2026, "POSITIVACAO", "Marca NESTLE / Positivacao", target, actual),
            GoalRecord.Create(goals.Id, "BOB", 8, 2026, "FATURAMENTO", "Marca NESTLE / Valor", 100, 9999),
            PppRecord.Create(ppp.Id, 2026, 8, "ANA", "MERCEARIA", 10, 2, 10),
            PppRecord.Create(ppp.Id, 2026, 8, "BOB", "FOREIGN", 1, 1, 99));
        await db.SaveChangesAsync();
        return db;
    }

    private static CommercialMovement Movement(Guid batch, string seller, string customer, decimal value, int day, string type = "VENDA") =>
        CommercialMovement.CreateFromImport(batch, new(2026, 8, day), seller, "NESTLE", "REDE", type, "CIDADE", customer, "CAFE", value, 1, 987654, customer, $"{seller}-{day}");

    private static ImportBatch Batch(ImportFileType type, string? checksum = null)
    {
        var batch = ImportBatch.Start(type, $"{type}.csv", checksum ?? Guid.NewGuid().ToString());
        batch.Complete("memory://import", 1, 0);
        return batch;
    }
}
