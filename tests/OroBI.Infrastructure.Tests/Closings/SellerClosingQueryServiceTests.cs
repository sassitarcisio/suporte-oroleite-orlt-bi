using Microsoft.EntityFrameworkCore;
using OroBI.Infrastructure.Closings;
using OroBI.Infrastructure.Persistence;
using OroBI.Domain.Closings;
using OroBI.Domain.Commercial;
using OroBI.Domain.Goals;
using OroBI.Domain.Imports;

namespace OroBI.Infrastructure.Tests.Closings;

public sealed class SellerClosingQueryServiceTests
{
    [Fact]
    public async Task Uses_latest_goal_values_batch_and_calculates_awards_per_brand()
    {
        var options = new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(nameof(Uses_latest_goal_values_batch_and_calculates_awards_per_brand))
            .Options;
        await using var db = new OroBiDbContext(options);
        var movementsBatch = CompletedBatch(ImportFileType.Power, "power.csv");
        var oldValuesBatch = CompletedBatch(ImportFileType.GoalValues, "old-values.csv");
        var currentValuesBatch = CompletedBatch(ImportFileType.GoalValues, "current-values.csv");
        var goalsBatch = CompletedBatch(ImportFileType.Goals, "goals.csv");
        db.AddRange(movementsBatch, oldValuesBatch, currentValuesBatch, goalsBatch);
        db.AddRange(
            ImportedClosingDefaults.Create(oldValuesBatch.Id, 1000m, 1m, 1200m, new Dictionary<string, decimal>()),
            ImportedClosingDefaults.Create(currentValuesBatch.Id, 1951m, 1m, 1200m, new Dictionary<string, decimal>()),
            GoalValueRecord.Create(oldValuesBatch.Id, "NESTLE", 999m, 999m, 999m, 2m),
            GoalValueRecord.Create(currentValuesBatch.Id, "NESTLE", 100m, 50m, 25m, 2m),
            GoalRecord.Create(goalsBatch.Id, "ANA", 8, 2026, "FATURAMENTO", "Marca NESTLE / Valor", 100m, 100m),
            GoalRecord.Create(goalsBatch.Id, "ANA", 8, 2026, "POSITIVACAO", "Marca NESTLE / Positivacao", 100m, 100m),
            CommercialMovement.CreateFromImport(movementsBatch.Id, new DateOnly(2026, 8, 1), "ANA", "NESTLE", "REDE A", "VENDA", "CIDADE", "CLIENTE", "PRODUTO", 1000m, 1m, 10m, "1", "1"),
            CommercialMovement.CreateFromImport(movementsBatch.Id, new DateOnly(2026, 8, 2), "ANA", "NESTLE", "REDE A", "TROCA DEV", "CIDADE", "CLIENTE", "PRODUTO", -10m, 1m, 10m, "1", "2"));
        await db.SaveChangesAsync();

        var result = await new SellerClosingQueryService(db).GetAsync("Ana", 2026, 8, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(175m, result.TotalAwards);
        Assert.Equal(100m, result.RevenueAward);
        Assert.Equal(50m, result.PositivityAward);
        Assert.Equal(25m, result.TradeAward);
        Assert.Equal(9.9m, result.Compensation.Commission);
        Assert.Equal(1960.9m, result.Compensation.TotalSalary);
    }

    private static ImportBatch CompletedBatch(ImportFileType type, string fileName)
    {
        var batch = ImportBatch.Start(type, fileName, Guid.NewGuid().ToString("N"));
        batch.Complete($"memory://{fileName}", 1, 0);
        return batch;
    }
}
