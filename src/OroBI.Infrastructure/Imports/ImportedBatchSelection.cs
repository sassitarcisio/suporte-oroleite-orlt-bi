using Microsoft.EntityFrameworkCore;
using OroBI.Domain.Imports;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Imports;

public static class ImportedBatchSelection
{
    // Keep the most recently processed copy of identical file content. Do not deduplicate
    // individual lines: repeated lines within one source file can be legitimate sales.
    public static async Task<Guid[]> GetDuplicateIdsAsync(OroBiDbContext dbContext, CancellationToken cancellationToken)
    {
        var batches = await dbContext.ImportBatches.AsNoTracking()
            .Where(batch => batch.Checksum != "" &&
                (batch.Status == ImportBatchStatus.Completed || batch.Status == ImportBatchStatus.CompletedWithErrors))
            .Select(batch => new { batch.Id, batch.FileType, batch.Checksum, batch.StartedAtUtc })
            .ToArrayAsync(cancellationToken);
        return batches.GroupBy(batch => (batch.FileType, Checksum: batch.Checksum.ToLowerInvariant()))
            .SelectMany(group => group.OrderByDescending(batch => batch.StartedAtUtc).ThenByDescending(batch => batch.Id).Skip(1))
            .Select(batch => batch.Id).ToArray();
    }
}
