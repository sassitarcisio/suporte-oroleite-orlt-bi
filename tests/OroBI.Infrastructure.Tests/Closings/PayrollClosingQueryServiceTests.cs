using Microsoft.EntityFrameworkCore;
using OroBI.Application.Analytics;
using OroBI.Domain.Closings;
using OroBI.Domain.Commercial;
using OroBI.Domain.Goals;
using OroBI.Domain.Imports;
using OroBI.Domain.Ppp;
using OroBI.Infrastructure.Closings;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Tests.Closings;

public sealed class PayrollClosingQueryServiceTests
{
    [Fact]
    public async Task Payroll_contains_nine_people_and_Tiago_copies_the_selected_coverage_at_standard_salary()
    {
        await using var db = CreateDatabase();
        var batch = SeedDefaults(db);
        db.AddRange(
            SellerClosingConfiguration.Create("RODRIGO KEHL", 2026, 8, 8888m, 2m, 1000m),
            Movement(batch, "RODRIGO KEHL", 12345.67m),
            Movement(batch, "MARCIO LUIZ DA ROSA", 500m));
        await db.SaveChangesAsync();

        var result = await new SellerClosingQueryService(db).GetPayrollAsync("RODRIGO", 2026, 8, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(9, result.SellerCount);
        Assert.Equal(new[] { "ANDERSON GONCALVES SOUZA", "MARCELO IVONEI DA ROSA", "MARCIO FERNANDES",
            "MARCIO LUIZ DA ROSA", "RAMON DO NASCIMENTO", "RODRIGO", "SUPERVISOR: DEIVID MANNES",
            "TIAGO MARTINS", "VALDIR ZACARIAS" }, result.Rows.Select(row => row.Seller));
        Assert.DoesNotContain(result.Rows, row => row.Seller.Contains("PAULO"));
        Assert.Equal("RODRIGO", result.CoverageSeller);
        var covered = Assert.Single(result.Rows, row => row.Seller == "RODRIGO");
        var tiago = Assert.Single(result.Rows, row => row.Seller == "TIAGO MARTINS");
        Assert.Equal(1951m, covered.BaseSalary);
        Assert.Equal(246.9134m, covered.Commission);
        Assert.Equal(covered with { Seller = "TIAGO MARTINS", Reference = "Cobertura de férias: RODRIGO" }, tiago);
        Assert.All(result.Rows.Where(row => row.Seller != "VALDIR ZACARIAS" && !row.Seller.StartsWith("SUPERVISOR:")),
            row => Assert.Equal(1951m, row.BaseSalary));
        Assert.Equal(2883.20m, result.Rows.Single(row => row.Seller.StartsWith("SUPERVISOR:")).BaseSalary);
        Assert.Equal(2662.50m, result.Rows.Single(row => row.Seller == "VALDIR ZACARIAS").BaseSalary);
    }

    [Fact]
    public async Task Paulo_contributes_to_payroll_team_average_but_has_zero_awards_in_supervisor_display()
    {
        await using var db = CreateDatabase();
        var batch = SeedDefaults(db);
        db.AddRange(
            PppRecord.Create(batch, 2026, 8, "VENDEDOR: ANDERSON GONCALVES SOUZA", "AS", 3, 1, 1),
            PppRecord.Create(batch, 2026, 8, "VENDEDOR: PAULO RICARDO LOPES", "AS", 2, 1, 1));
        await db.SaveChangesAsync();
        var service = new SellerClosingQueryService(db);

        var supervisor = await service.GetAsync("DEIVID MANNES", 2026, 8, CancellationToken.None);
        var payroll = await service.GetPayrollAsync("MARCIO LUIZ DA ROSA", 2026, 8, CancellationToken.None);

        Assert.NotNull(supervisor?.Supervisor);
        Assert.NotNull(payroll);
        Assert.Equal(7, supervisor.Supervisor.Team.Count);
        var paulo = Assert.Single(supervisor.Supervisor.Team, member => member.Seller == "PAULO RICARDO LOPES");
        Assert.False(paulo.IncludedInPayroll);
        Assert.Equal(0m, paulo.TotalAward);
        var displayAverage = supervisor.Supervisor.TeamAverageAward;
        var payrollAverage = supervisor.Supervisor.PayrollTeamAverageAward;
        Assert.Equal(47.62m, decimal.Round(displayAverage, 2));
        Assert.Equal(119.05m, decimal.Round(payrollAverage, 2));
        Assert.Equal(71.428571428571m, decimal.Round(payrollAverage - displayAverage, 12));
        Assert.NotEqual(decimal.Round(displayAverage, 2), displayAverage);
        Assert.Equal(displayAverage, supervisor.RevenueAward);
        Assert.Equal(payrollAverage, payroll.Rows.Single(row => row.Seller.StartsWith("SUPERVISOR:")).GoalAward);
    }

    [Fact]
    public async Task Payroll_awards_only_the_eight_reference_brands_even_when_other_brands_reach_their_goals()
    {
        await using var db = CreateDatabase();
        var batch = SeedDefaults(db);
        string[] brands = ["NESTLE", "GALBANI", "ZINHO", "LIFE", "PECCIN", "NOTCO", "VISCONTI", "BAUDUCCO", "LIGHTSWEET", "ACAI FUTURO"];
        foreach (var brand in brands)
        {
            db.AddRange(GoalValueRecord.Create(batch, brand, 20m, 10m, 5m, 2m),
                Goal(batch, brand, "FATURAMENTO"), Goal(batch, brand, "POSITIVACAO"),
                Movement(batch, "ANDERSON GONCALVES SOUZA", 100m, brand: brand));
        }
        await db.SaveChangesAsync();
        var service = new SellerClosingQueryService(db);

        var payroll = await service.GetPayrollAsync("MARCIO LUIZ DA ROSA", 2026, 8, CancellationToken.None);
        var generic = await service.GetAsync("ANDERSON GONCALVES SOUZA", 2026, 8, CancellationToken.None);

        Assert.NotNull(payroll);
        Assert.NotNull(generic);
        Assert.Equal(280m, payroll.Rows.Single(row => row.Seller == "ANDERSON GONCALVES SOUZA").GoalAward);
        Assert.Equal(350m, generic.TotalAwards);
    }

    [Fact]
    public async Task Payroll_requires_an_exact_brand_goal_and_does_not_pay_missing_goal_trade_prizes()
    {
        await using var db = CreateDatabase();
        var batch = SeedDefaults(db);
        db.AddRange(
            GoalValueRecord.Create(batch, "NESTLE", 20m, 10m, 5m, 2m),
            GoalValueRecord.Create(batch, "GALBANI", 20m, 10m, 5m, 2m),
            GoalValueRecord.Create(batch, "NOTCO", 20m, 10m, 5m, 2m),
            Goal(batch, "NESTLE PLUS", "FATURAMENTO"),
            Goal(batch, "NOTCO", "FATURAMENTO"),
            Movement(batch, "ANDERSON GONCALVES SOUZA", 100m));
        await db.SaveChangesAsync();

        var payroll = await new SellerClosingQueryService(db).GetPayrollAsync("MARCIO LUIZ DA ROSA", 2026, 8, CancellationToken.None);

        Assert.NotNull(payroll);
        Assert.Equal(25m, payroll.Rows.Single(row => row.Seller == "ANDERSON GONCALVES SOUZA").GoalAward);
    }

    [Fact]
    public async Task Supervisor_union_counts_overlapping_network_rows_once_but_retains_each_commission_scope()
    {
        await using var db = CreateDatabase();
        var batch = SeedDefaults(db);
        db.AddRange(
            Movement(batch, "DEIVID MANNES", 100m, group: "BISTEK"),
            Movement(batch, "ANDERSON GONCALVES SOUZA", 200m, group: "GIASSI"),
            Movement(batch, "OUTRO", 300m, group: "BISTEK"),
            Movement(batch, "ANDERSON GONCALVES SOUZA", -10m, "TROCA DEV", "GIASSI"),
            Movement(batch, "DEIVID MANNES", 50m, "BONIFICACAO", "BISTEK"),
            Movement(batch, "OPERACAO BAUDUCCO", 10000m, group: "BISTEK"),
            Movement(batch, "FORA DO ESCOPO", 5000m));
        await db.SaveChangesAsync();

        var result = await new SellerClosingQueryService(db).GetAsync("DEIVID MANNES", 2026, 8, CancellationToken.None);

        Assert.NotNull(result?.Supervisor);
        Assert.Equal(590m, result.Monthly.Revenue);
        Assert.Equal(4, result.Monthly.MovementCount);
        Assert.Equal(10m, result.Monthly.TradeValue);
        Assert.Equal(100m, result.Supervisor.Operations.Single(row => row.Key == "own").Revenue);
        Assert.Equal(190m, result.Supervisor.Operations.Single(row => row.Key == "team").Revenue);
        Assert.Equal(590m, result.Supervisor.Operations.Single(row => row.Key == "networks").Revenue);
        Assert.Equal(590m, result.Supervisor.Operations.Single(row => row.Key == "total").Revenue);
        Assert.Equal(2.17m, result.Compensation.Commission);
        Assert.Equal(3000m, result.TradeAward);
    }

    [Fact]
    public async Task Payroll_Deivid_base_sums_commission_operations_while_supervisor_trade_base_is_the_union()
    {
        await using var db = CreateDatabase();
        var batch = SeedDefaults(db);
        db.AddRange(
            Movement(batch, "DEIVID MANNES", 100m, group: "BISTEK"),
            Movement(batch, "ANDERSON GONCALVES SOUZA", 200m, group: "GIASSI"),
            Movement(batch, "OUTRO", 300m, group: "BISTEK"),
            Movement(batch, "ANDERSON GONCALVES SOUZA", -10m, "TROCA DEV", "GIASSI"));
        await db.SaveChangesAsync();
        var service = new SellerClosingQueryService(db);

        var payroll = await service.GetPayrollAsync("MARCIO LUIZ DA ROSA", 2026, 8, CancellationToken.None);
        var supervisor = await service.GetAsync("DEIVID MANNES", 2026, 8, CancellationToken.None);

        Assert.NotNull(payroll);
        Assert.NotNull(supervisor);
        var row = Assert.Single(payroll.Rows, item => item.Seller == "SUPERVISOR: DEIVID MANNES");
        Assert.Equal(880m, row.Revenue);
        Assert.Equal(590m, supervisor.Monthly.TradeRevenueBase);
        Assert.Equal(10m, supervisor.Monthly.TradeValue);
        Assert.Equal(2.17m, row.Commission);
        Assert.Equal(3000m, row.TradeAward);
    }

    [Fact]
    public async Task Payroll_is_unavailable_when_Paulos_Ppp_ceiling_is_missing_even_with_all_six_payroll_sellers_configured()
    {
        await using var db = CreateDatabase();
        SeedDefaults(db, pppMaximumAward: null);
        string[] configuredSellers = ["ANDERSON GONCALVES SOUZA", "MARCELO IVONEI DA ROSA", "MARCIO FERNANDES",
            "MARCIO LUIZ DA ROSA", "RAMON DO NASCIMENTO", "RODRIGO KEHL"];
        foreach (var seller in configuredSellers)
            db.Add(SellerClosingConfiguration.Create(seller, 2026, 8, 1951m, 1m, 1000m));
        await db.SaveChangesAsync();
        var service = new SellerClosingQueryService(db);

        var standard = await service.GetAsync("ANDERSON GONCALVES SOUZA", 2026, 8, CancellationToken.None);
        var payroll = await service.GetPayrollAsync("MARCIO LUIZ DA ROSA", 2026, 8, CancellationToken.None);
        var supervisor = await service.GetAsync("DEIVID MANNES", 2026, 8, CancellationToken.None);

        Assert.NotNull(standard);
        Assert.Null(payroll);
        Assert.Null(supervisor);
    }

    [Fact]
    public async Task Canonical_seller_names_in_movements_goals_and_Ppp_produce_the_same_payroll_and_supervisor_awards()
    {
        await using var db = CreateDatabase();
        var batch = SeedDefaults(db);
        const string seller = "ANDERSON GONCALVES SOUZA";
        db.AddRange(
            GoalValueRecord.Create(batch, "NESTLE", 100m, 50m, 25m, 2m),
            GoalRecord.Create(batch, seller, 8, 2026, "FATURAMENTO", "Marca NESTLE / Valor", 100m, 100m),
            PppRecord.Create(batch, 2026, 8, seller, "AS", 2, 1, 1),
            CommercialMovement.CreateFromImport(batch, new DateOnly(2026, 8, 1), seller, "NESTLE", "OUTRA REDE",
                "VENDA", "CIDADE", "CLIENTE", "PRODUTO", 1000m, 1m, 1m, "123", "DOC-CANONICAL"));
        await db.SaveChangesAsync();
        var service = new SellerClosingQueryService(db);

        var payroll = await service.GetPayrollAsync("MARCIO LUIZ DA ROSA", 2026, 8, CancellationToken.None);
        var supervisor = await service.GetAsync("DEIVID MANNES", 2026, 8, CancellationToken.None);
        var standard = await service.GetAsync(seller, 2026, 8, CancellationToken.None);

        Assert.NotNull(payroll);
        Assert.NotNull(supervisor?.Supervisor);
        Assert.NotNull(standard);
        var row = payroll.Rows.Single(item => item.Seller == seller);
        var member = supervisor.Supervisor.Team.Single(item => item.Seller == seller);
        Assert.Equal(1000m, row.Revenue);
        Assert.Equal(10m, row.Commission);
        Assert.Equal(500m, row.PppAward);
        Assert.Equal(125m, row.GoalAward);
        Assert.Equal(625m, member.TotalAward);
        Assert.Equal(1000m, member.Sales.Revenue);
        Assert.Equal(1000m, supervisor.Monthly.Revenue);
        Assert.Equal(625m, standard.TotalAwards);
        Assert.Equal(10m, standard.Compensation.Commission);
    }

    [Fact]
    public async Task Payroll_requires_imported_goal_values_even_when_every_seller_has_explicit_configuration()
    {
        await using var db = CreateDatabase();
        string[] sellers = ["ANDERSON GONCALVES SOUZA", "MARCELO IVONEI DA ROSA", "MARCIO FERNANDES",
            "MARCIO LUIZ DA ROSA", "RAMON DO NASCIMENTO", "RODRIGO KEHL", "PAULO RICARDO LOPES",
            "DEIVID MANNES", "VALDIR ZACARIAS"];
        foreach (var seller in sellers)
            db.Add(SellerClosingConfiguration.Create(seller, 2026, 8, 1951m, 1m, 1000m));
        await db.SaveChangesAsync();

        var payroll = await new SellerClosingQueryService(db)
            .GetPayrollAsync("MARCIO LUIZ DA ROSA", 2026, 8, CancellationToken.None);

        Assert.Null(payroll);
    }

    [Theory]
    [InlineData("DEIVID MANNES", 0)]
    [InlineData("DEIVID MANNES", -100)]
    [InlineData("VALDIR ZACARIAS", 0)]
    [InlineData("VALDIR ZACARIAS", -100)]
    public async Task Special_closings_do_not_pay_trade_awards_without_positive_revenue(string seller, int revenue)
    {
        await using var db = CreateDatabase();
        var batch = SeedDefaults(db);
        if (revenue != 0) db.Add(Movement(batch, "DEIVID MANNES", revenue, "DEVOLUCAO"));
        await db.SaveChangesAsync();

        var result = await new SellerClosingQueryService(db).GetAsync(seller, 2026, 8, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0m, result.TradeAward);
    }

    [Theory]
    [InlineData("DEIVID MANNES", 1254, 5000)]
    [InlineData("DEIVID MANNES", 1255, 3000)]
    [InlineData("DEIVID MANNES", 2254, 2000)]
    [InlineData("DEIVID MANNES", 2255, 0)]
    [InlineData("VALDIR ZACARIAS", 2004, 5000)]
    [InlineData("VALDIR ZACARIAS", 2005, 3000)]
    [InlineData("VALDIR ZACARIAS", 4004, 2000)]
    [InlineData("VALDIR ZACARIAS", 4005, 0)]
    public async Task Special_trade_award_uses_displayed_two_decimal_rate_at_thresholds(string seller, int trade, int award)
    {
        await using var db = CreateDatabase();
        var batch = SeedDefaults(db);
        db.AddRange(Movement(batch, "DEIVID MANNES", 100000m + trade),
            Movement(batch, "DEIVID MANNES", -trade, "TROCA DEV"));
        await db.SaveChangesAsync();

        var result = await new SellerClosingQueryService(db).GetAsync(seller, 2026, 8, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal((decimal)award, result.TradeAward);
    }

    [Fact]
    public async Task Payroll_keeps_full_precision_commissions_before_aggregating_Valdir_and_coverage()
    {
        await using var db = CreateDatabase();
        var batch = SeedDefaults(db);
        db.Add(Movement(batch, "MARCIO LUIZ DA ROSA", 1.49m));
        await db.SaveChangesAsync();
        var service = new SellerClosingQueryService(db);

        var payroll = await service.GetPayrollAsync("MARCIO LUIZ DA ROSA", 2026, 8, CancellationToken.None);
        var valdir = await service.GetAsync("VALDIR ZACARIAS", 2026, 8, CancellationToken.None);

        Assert.NotNull(payroll);
        Assert.NotNull(valdir);
        Assert.Equal(0.033525m, payroll.TotalCommission);
        Assert.Equal(0.00149m, payroll.Rows.Single(row => row.Seller == "VALDIR ZACARIAS").Commission);
        Assert.Equal(0m, valdir.Compensation.Commission);
        Assert.Equal(0.03m, decimal.Round(payroll.TotalCommission, 2));
        Assert.Equal(0.02m, payroll.Rows.Sum(row => decimal.Round(row.Commission, 2)));
    }

    [Fact]
    public async Task Paulo_cannot_be_selected_as_Tiagos_payroll_coverage()
    {
        await using var db = CreateDatabase();
        await Assert.ThrowsAsync<ArgumentException>(() => new SellerClosingQueryService(db)
            .GetPayrollAsync("PAULO RICARDO LOPES", 2026, 8, CancellationToken.None));
    }

    private static OroBiDbContext CreateDatabase() => new(new DbContextOptionsBuilder<OroBiDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Guid SeedDefaults(OroBiDbContext db, decimal? pppMaximumAward = 1000m)
    {
        var batch = ImportBatch.Start(ImportFileType.GoalValues, "settings.csv", Guid.NewGuid().ToString("N"));
        batch.Complete("memory://settings.csv", 1, 0);
        db.AddRange(batch, ImportedClosingDefaults.Create(batch.Id, 9999m, 1m, pppMaximumAward,
            new Dictionary<string, decimal> { ["DEIVID MANNES"] = 2883.20m, ["VALDIR ZACARIAS"] = 2662.50m }));
        return batch.Id;
    }

    private static GoalRecord Goal(Guid batch, string brand, string type) => GoalRecord.Create(batch,
        "VENDEDOR: ANDERSON GONCALVES SOUZA", 8, 2026, type,
        $"Marca {brand} / {(type == "FATURAMENTO" ? "Valor" : "Positivacao")}", 100m, 100m);

    private static CommercialMovement Movement(Guid batch, string seller, decimal amount, string type = "VENDA",
        string group = "OUTRA REDE", string brand = "NESTLE") => CommercialMovement.CreateFromImport(batch,
            new DateOnly(2026, 8, 1), SellerAliasCatalog.ResolveImportedName(seller), brand, group, type,
            "CIDADE", "CLIENTE", "PRODUTO", amount, 1m, 1m, "123", Guid.NewGuid().ToString("N"));
}
