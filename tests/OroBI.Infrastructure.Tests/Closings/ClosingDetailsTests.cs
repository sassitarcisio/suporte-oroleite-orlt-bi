using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OroBI.Domain.Closings;
using OroBI.Domain.Commercial;
using OroBI.Domain.Goals;
using OroBI.Domain.Imports;
using OroBI.Domain.Ppp;
using OroBI.Infrastructure.Closings;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Tests.Closings;

public sealed class ClosingDetailsTests
{
    [Fact]
    public async Task Returns_month_documents_segments_and_brand_targets_without_mixing_sellers_or_periods()
    {
        await using var db = CreateDatabase();
        var batch = SeedConfiguration(db);
        db.AddRange(
            Movement(batch, "ANA", 8, "VENDA", 600m, "NF1"),
            Movement(batch, "ANA", 8, "VENDA", 400m, "NF1"),
            Movement(batch, "ANA", 8, "TROCA DEV", -20m, "NF2"),
            Movement(batch, "ANA", 8, "BONIFICACAO", 100m, "NF3"),
            Movement(batch, "ANA", 8, "VENDA", 20m, ""),
            Movement(batch, "BRUNO", 8, "VENDA", 9000m, "NF4"),
            Movement(batch, "ANA", 7, "VENDA", 8000m, "NF5"),
            PppRecord.Create(batch, 2026, 8, "ANA", "MERCADO", 10, 4, 30),
            PppRecord.Create(batch, 2026, 8, "ANA", "SEM BASE", 0, 4, 3),
            PppRecord.Create(batch, 2026, 7, "ANA", "OUTRO MES", 1, 1, 1),
            GoalRecord.Create(batch, "ANA", 8, 2026, "FATURAMENTO", "Marca NESTLE / Valor", 2000m, 1000m),
            GoalRecord.Create(batch, "ANA", 8, 2026, "POSITIVACAO", "Marca NESTLE / Positivacao", 20m, 20m),
            GoalValueRecord.Create(batch, "NESTLE", 100m, 50m, 25m, 2m));
        await db.SaveChangesAsync();

        var result = await new SellerClosingQueryService(db).GetAsync("Ana", 2026, 8, CancellationToken.None);
        var json = JsonSerializer.SerializeToElement(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var monthly = json.GetProperty("monthly");
        Assert.Equal(1100m, monthly.GetProperty("revenue").GetDecimal());
        Assert.Equal(1000m, monthly.GetProperty("commissionableRevenue").GetDecimal());
        Assert.Equal(20m, monthly.GetProperty("tradeValue").GetDecimal());
        Assert.Equal(2m, monthly.GetProperty("tradePercent").GetDecimal());
        Assert.Equal(3, monthly.GetProperty("documentCount").GetInt32());
        Assert.Equal(5, monthly.GetProperty("movementCount").GetInt32());
        Assert.Equal(1, monthly.GetProperty("customerCount").GetInt32());
        var documents = monthly.GetProperty("documents").EnumerateArray().ToArray();
        Assert.Equal(1000m, documents.Single(doc => doc.GetProperty("documentNumber").GetString() == "NF1").GetProperty("totalValue").GetDecimal());
        Assert.Equal(2000m, json.GetProperty("compensation").GetProperty("baseSalary").GetDecimal());
        Assert.Equal(10m, result!.Compensation.Commission);
        Assert.Equal(2985m, json.GetProperty("total").GetDecimal());
        var segments = json.GetProperty("pppSegments").EnumerateArray().ToArray();
        Assert.Equal(2, segments.Length);
        Assert.Equal(75m, segments[0].GetProperty("achievementPercent").GetDecimal());
        Assert.Equal(JsonValueKind.Null, segments[1].GetProperty("achievementPercent").ValueKind);
        Assert.Equal(75m, result.Ppp.MeanPercent);
        var brand = json.GetProperty("brandAwards")[0];
        Assert.Equal(2000m, brand.GetProperty("revenueGoal").GetDecimal());
        Assert.Equal(1000m, brand.GetProperty("revenueActual").GetDecimal());
        Assert.Equal(50m, brand.GetProperty("revenueAchievedPercent").GetDecimal());
        Assert.Equal(20m, brand.GetProperty("positivityGoal").GetDecimal());
        Assert.Equal(20m, brand.GetProperty("positivityActual").GetDecimal());
        Assert.Equal(100m, brand.GetProperty("positivityAchievedPercent").GetDecimal());
        Assert.Equal(20m, brand.GetProperty("tradeValue").GetDecimal());
        Assert.Equal(2m, brand.GetProperty("tradeGoalPercent").GetDecimal());
        Assert.Equal(75m, brand.GetProperty("totalAward").GetDecimal());
    }

    [Fact]
    public async Task Returns_empty_details_and_zero_percentages_for_month_without_records()
    {
        await using var db = CreateDatabase();
        SeedConfiguration(db);
        await db.SaveChangesAsync();
        var result = await new SellerClosingQueryService(db).GetAsync("ANA", 2026, 8, CancellationToken.None);
        var json = JsonSerializer.SerializeToElement(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var monthly = json.GetProperty("monthly");
        Assert.Equal(0m, monthly.GetProperty("tradePercent").GetDecimal());
        Assert.Equal(0, monthly.GetProperty("documentCount").GetInt32());
        Assert.Empty(monthly.GetProperty("documents").EnumerateArray());
        Assert.Empty(json.GetProperty("pppSegments").EnumerateArray());
        Assert.Empty(json.GetProperty("brandAwards").EnumerateArray());
        Assert.Equal(2000m, json.GetProperty("total").GetDecimal());
    }

    private static OroBiDbContext CreateDatabase() => new(new DbContextOptionsBuilder<OroBiDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Guid SeedConfiguration(OroBiDbContext db)
    {
        var batch = ImportBatch.Start(ImportFileType.GoalValues, "values.csv", Guid.NewGuid().ToString());
        batch.Complete("memory://values.csv", 1, 0);
        db.AddRange(batch, ImportedClosingDefaults.Create(batch.Id, 2000m, 1m, 1200m, new Dictionary<string, decimal>()));
        return batch.Id;
    }

    private static CommercialMovement Movement(Guid batch, string seller, int month, string type, decimal value, string document) =>
        CommercialMovement.CreateFromImport(batch, new DateOnly(2026, month, 1), seller, "NESTLE", "REDE", type, "CIDADE", "CLIENTE", "PRODUTO", value, 1m, 10m, "1", document);
}
