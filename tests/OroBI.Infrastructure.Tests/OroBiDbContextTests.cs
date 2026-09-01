using Microsoft.EntityFrameworkCore;
using OroBI.Domain.Commercial;
using OroBI.Domain.Imports;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Tests;

public sealed class OroBiDbContextTests
{
    [Fact]
    public async Task Saves_a_movement_linked_to_its_import_batch()
    {
        var options = new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(nameof(Saves_a_movement_linked_to_its_import_batch))
            .Options;

        await using var db = new OroBiDbContext(options);
        var batch = ImportBatch.Start(ImportFileType.Power, "power.csv", "abc");
        db.ImportBatches.Add(batch);
        db.CommercialMovements.Add(CommercialMovement.Create(
            batch.Id,
            new DateOnly(2026, 1, 1),
            "ANA",
            "VENDA",
            125m,
            2m));

        await db.SaveChangesAsync();

        var movement = await db.CommercialMovements.SingleAsync();
        Assert.Equal(batch.Id, movement.ImportBatchId);
    }
}
