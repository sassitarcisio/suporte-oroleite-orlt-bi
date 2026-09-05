using Microsoft.EntityFrameworkCore;
using OroBI.Domain.Closings;
using OroBI.Domain.Commercial;
using OroBI.Domain.Goals;
using OroBI.Domain.Imports;
using OroBI.Infrastructure.Closings;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Tests.Closings;

public sealed class SpecialClosingQueryServiceTests
{
    [Fact]
    public async Task Calculates_Valdir_from_company_scope_without_Bauducco_or_bonus()
    {
        var options = new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(nameof(Calculates_Valdir_from_company_scope_without_Bauducco_or_bonus))
            .Options;
        await using var db = new OroBiDbContext(options);
        var valuesBatch = CompletedBatch(ImportFileType.GoalValues, "values.csv");
        var movementsBatch = CompletedBatch(ImportFileType.Power, "power.csv");
        db.AddRange(valuesBatch, movementsBatch);
        db.Add(ImportedClosingDefaults.Create(
            valuesBatch.Id,
            1951m,
            1m,
            1200m,
            new Dictionary<string, decimal> { ["VENDEDOR: VALDIR ZACARIAS"] = 4000m }));
        db.AddRange(
            Movement(movementsBatch.Id, "ANA", "VENDA", 1000m),
            Movement(movementsBatch.Id, "ANA", "TROCA DEV", -10m),
            Movement(movementsBatch.Id, "ANA", "BONIFICACAO", 100m),
            Movement(movementsBatch.Id, "OPERACAO BAUDUCCO", "VENDA", 1000000m));
        await db.SaveChangesAsync();

        var result = await new SellerClosingQueryService(db).GetAsync("Valdir Zacarias", 2026, 8, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0.99m, result.Compensation.Commission);
        Assert.Equal(4000.99m, result.Compensation.TotalSalary);
        Assert.Equal(5000m, result.TradeAward);
        Assert.Equal(5000m, result.TotalAwards);
        Assert.Equal("company-excluding-bauducco", result.Monthly.Scope);
        Assert.Equal(990m, result.Monthly.Revenue);
        Assert.Equal(990m, result.Monthly.CommissionableRevenue);
        Assert.Equal(10m, result.Monthly.TradeValue);
        Assert.Equal(10m / 990m * 100m, result.Monthly.TradePercent);
        Assert.Equal(2, result.Monthly.DocumentCount);
        Assert.Equal(4000m, result.Compensation.BaseSalary);
        Assert.Equal(9000.99m, result.Total);
        Assert.Empty(result.PppSegments);
        Assert.Empty(result.BrandAwards);
    }

    [Fact]
    public async Task Calculates_Deivid_from_own_team_and_network_scopes()
    {
        var options = new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(nameof(Calculates_Deivid_from_own_team_and_network_scopes))
            .Options;
        await using var db = new OroBiDbContext(options);
        var valuesBatch = CompletedBatch(ImportFileType.GoalValues, "values.csv");
        var movementsBatch = CompletedBatch(ImportFileType.Power, "power.csv");
        var goalsBatch = CompletedBatch(ImportFileType.Goals, "goals.csv");
        db.AddRange(valuesBatch, movementsBatch, goalsBatch);
        db.Add(ImportedClosingDefaults.Create(
            valuesBatch.Id,
            1951m,
            1m,
            1200m,
            new Dictionary<string, decimal> { ["SUPERVISOR: DEIVID MANNES"] = 3000m }));
        db.AddRange(
            GoalValueRecord.Create(valuesBatch.Id, "NESTLE", 100m, 50m, 25m, 2m),
            GoalRecord.Create(goalsBatch.Id, "ANDERSON GONCALVES SOUZA", 8, 2026, "FATURAMENTO", "Marca NESTLE / Valor", 100m, 100m),
            GoalRecord.Create(goalsBatch.Id, "ANDERSON GONCALVES SOUZA", 8, 2026, "POSITIVACAO", "Marca NESTLE / Positivacao", 100m, 100m),
            Movement(movementsBatch.Id, "SUPERVISOR: DEIVID MANNES", "VENDA", 10000m),
            Movement(movementsBatch.Id, "VENDEDOR: ANDERSON GONCALVES SOUZA", "VENDA", 200000m),
            Movement(movementsBatch.Id, "OUTRO VENDEDOR", "VENDA", 50000m, "BISTEK"),
            Movement(movementsBatch.Id, "OPERACAO BAUDUCCO", "VENDA", 1000000m));
        await db.SaveChangesAsync();

        var result = await new SellerClosingQueryService(db).GetAsync("Deivid Mannes", 2026, 8, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(475m, result.Compensation.Commission);
        Assert.Equal(3475m, result.Compensation.TotalSalary);
        Assert.Equal(325m / 7m, result.RevenueAward);
        Assert.Equal(5000m, result.TradeAward);
        Assert.Equal(5000m + 325m / 7m, result.TotalAwards);
        Assert.Equal("company", result.Monthly.Scope);
        Assert.Equal(1260000m, result.Monthly.Revenue);
        Assert.Equal(4, result.Monthly.DocumentCount);
        Assert.Equal(0m, result.Monthly.TradePercent);
        Assert.Equal(3000m, result.Compensation.BaseSalary);
        Assert.Empty(result.PppSegments);
        Assert.Empty(result.BrandAwards);
    }

    private static CommercialMovement Movement(Guid batchId, string seller, string type, decimal amount, string group = "OUTRA REDE") =>
        CommercialMovement.CreateFromImport(batchId, new DateOnly(2026, 8, 1), seller, "NESTLE", group, type, "CIDADE", "CLIENTE", "PRODUTO", amount, 1m, 10m, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));

    private static ImportBatch CompletedBatch(ImportFileType type, string fileName)
    {
        var batch = ImportBatch.Start(type, fileName, Guid.NewGuid().ToString("N"));
        batch.Complete($"memory://{fileName}", 1, 0);
        return batch;
    }
}
