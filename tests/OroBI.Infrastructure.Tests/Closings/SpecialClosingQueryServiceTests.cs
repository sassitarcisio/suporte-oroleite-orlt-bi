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
    public async Task Calculates_Valdir_commission_with_bonus_but_trade_rate_without_bonus()
    {
        var options = new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(nameof(Calculates_Valdir_commission_with_bonus_but_trade_rate_without_bonus))
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
        Assert.Equal(1.09m, result.Compensation.Commission);
        Assert.Equal(4001.09m, result.Compensation.TotalSalary);
        Assert.Equal(5000m, result.TradeAward);
        Assert.Equal(5000m, result.TotalAwards);
        Assert.Equal("company-excluding-bauducco", result.Monthly.Scope);
        Assert.Equal(1090m, result.Monthly.Revenue);
        Assert.Equal(1090m, result.Monthly.CommissionableRevenue);
        Assert.Equal(10m, result.Monthly.TradeValue);
        Assert.Equal(10m / 990m * 100m, result.Monthly.TradePercent);
        Assert.Equal(3, result.Monthly.DocumentCount);
        Assert.Equal(4000m, result.Compensation.BaseSalary);
        Assert.Equal(9001.09m, result.Total);
        Assert.Empty(result.PppSegments);
        Assert.Empty(result.BrandAwards);
    }

    [Fact]
    public async Task Matches_official_August_Valdir_statement_with_four_identical_imports()
    {
        await using var db = new OroBiDbContext(new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(nameof(Matches_official_August_Valdir_statement_with_four_identical_imports)).Options);
        var values = CompletedBatch(ImportFileType.GoalValues, "values.csv");
        db.AddRange(values, ImportedClosingDefaults.Create(values.Id, 1951m, 1m, 1000m,
            new Dictionary<string, decimal> { ["VALDIR ZACARIAS"] = 2662.50m }));
        for (var i = 0; i < 4; i++)
        {
            var batch = ImportBatch.Start(ImportFileType.Power, $"power-{i}.csv", "same-file-checksum");
            batch.Complete($"memory://power-{i}.csv", 8, 0);
            db.Add(batch);
            db.AddRange(
                Movement(batch.Id, "ANA", "VENDA", 4946799.94m),
                Movement(batch.Id, "ANA", "DEVOLUCAO", -111226.03m),
                Movement(batch.Id, "ANA", "BONIFICACAO", 10800.17m),
                Movement(batch.Id, "ANA", "TROCA", 4443.07m),
                Movement(batch.Id, "ANA", "DESC BOLETO", -44042.25m),
                Movement(batch.Id, "ANA", "TROCA DEV", -230467.41m),
                Movement(batch.Id, "ANA", "DEVOL ENT", -18841.71m),
                Movement(batch.Id, "OPERACAO BAUDUCCO", "VENDA", 1000000m));
        }
        await db.SaveChangesAsync();

        var result = await new SellerClosingQueryService(db).GetAsync("VALDIR ZACARIAS", 2026, 8, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(4557465.78m, result.Monthly.Revenue);
        Assert.Equal(4557465.78m, result.Monthly.CommissionableRevenue);
        Assert.Equal(4546665.61m, result.Monthly.TradeRevenueBase);
        Assert.Equal(234910.48m, result.Monthly.TradeValue);
        Assert.Equal(5.17m, decimal.Round(result.Monthly.TradePercent, 2));
        Assert.Equal(4557.47m, result.Compensation.Commission);
        Assert.Equal(2662.50m, result.Compensation.BaseSalary);
        Assert.Equal(0m, result.TradeAward);
        Assert.Equal(7219.97m, result.Total);
        Assert.Equal(7, result.Monthly.MovementCount);
        Assert.Equal(32, await db.CommercialMovements.CountAsync()); // Original imports remain available for audit.
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
            GoalValueRecord.Create(valuesBatch.Id, "NESTLE", 140m, 60m, 25m, 2m),
            GoalRecord.Create(goalsBatch.Id, "VENDEDOR: ANDERSON GONCALVES SOUZA", 8, 2026, "FATURAMENTO", "Marca NESTLE / Valor", 100m, 100m),
            GoalRecord.Create(goalsBatch.Id, "VENDEDOR: ANDERSON GONCALVES SOUZA", 8, 2026, "POSITIVACAO", "Marca NESTLE / Positivacao", 100m, 100m),
            Movement(movementsBatch.Id, "SUPERVISOR: DEIVID MANNES", "VENDA", 10000m),
            Movement(movementsBatch.Id, "VENDEDOR: ANDERSON GONCALVES SOUZA", "VENDA", 200000m),
            Movement(movementsBatch.Id, "OUTRO VENDEDOR", "VENDA", 50000m, "BISTEK"),
            Movement(movementsBatch.Id, "OPERACAO BAUDUCCO", "VENDA", 1000000m));
        await db.SaveChangesAsync();

        var result = await new SellerClosingQueryService(db).GetAsync("Deivid Mannes", 2026, 8, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(475m, result.Compensation.Commission);
        Assert.Equal(3475m, result.Compensation.TotalSalary);
        Assert.Equal(225m / 7m, result.RevenueAward);
        Assert.Equal(5000m, result.TradeAward);
        Assert.Equal(5000m + 225m / 7m, result.TotalAwards);
        Assert.Equal("supervisor-union", result.Monthly.Scope);
        Assert.Equal(260000m, result.Monthly.Revenue);
        Assert.Equal(3, result.Monthly.DocumentCount);
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
