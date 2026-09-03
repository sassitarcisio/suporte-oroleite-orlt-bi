using Microsoft.EntityFrameworkCore;
using OroBI.Domain.Commercial;
using OroBI.Domain.Imports;
using OroBI.Infrastructure.Analytics;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Tests.Analytics;

public sealed class CommercialFilterOptionsQueryServiceTests
{
    [Fact]
    public async Task Excludes_inactive_zzz_brands_from_filter_options()
    {
        var options = new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(nameof(Excludes_inactive_zzz_brands_from_filter_options))
            .Options;
        await using var db = new OroBiDbContext(options);
        var batchId = ImportBatch.Start(ImportFileType.Power, "power.csv", "abc").Id;
        db.CommercialMovements.AddRange(
            CommercialMovement.CreateFromImport(batchId, new DateOnly(2026, 8, 1), "ANA", "OROLEITE", "LEITES", "VENDA", "GOIANIA", "Cliente", "Leite", 10m, 1m, 1m, "1", "1"),
            CommercialMovement.CreateFromImport(batchId, new DateOnly(2026, 8, 1), "ANA", "ZZZ - INATIVO", "LEITES", "VENDA", "GOIANIA", "Cliente", "Leite", 10m, 1m, 1m, "1", "2"));
        await db.SaveChangesAsync();

        var result = await new CommercialFilterOptionsQueryService(db).GetAsync(CancellationToken.None);

        Assert.Equal(["OROLEITE"], result.Brands);
    }
}
