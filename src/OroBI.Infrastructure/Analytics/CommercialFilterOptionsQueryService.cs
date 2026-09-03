using Microsoft.EntityFrameworkCore;
using OroBI.Application.Analytics;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Analytics;

public sealed class CommercialFilterOptionsQueryService(OroBiDbContext dbContext) : ICommercialFilterOptionsQueryService
{
    public async Task<CommercialFilterOptions> GetAsync(CancellationToken cancellationToken)
    {
        var brands = await dbContext.CommercialMovements.AsNoTracking().Select(item => item.Brand).Distinct().OrderBy(item => item).ToArrayAsync(cancellationToken);
        var groups = await dbContext.CommercialMovements.AsNoTracking().Select(item => item.Group).Distinct().OrderBy(item => item).ToArrayAsync(cancellationToken);
        var cities = await dbContext.CommercialMovements.AsNoTracking().Select(item => item.City).Distinct().OrderBy(item => item).ToArrayAsync(cancellationToken);
        var movementTypes = await dbContext.CommercialMovements.AsNoTracking().Select(item => item.MovementType).Distinct().OrderBy(item => item).ToArrayAsync(cancellationToken);

        return new CommercialFilterOptions(
            brands.Where(item => !string.IsNullOrWhiteSpace(item)).ToArray(),
            groups.Where(item => !string.IsNullOrWhiteSpace(item)).ToArray(),
            cities.Where(item => !string.IsNullOrWhiteSpace(item)).ToArray(),
            movementTypes.Where(item => !string.IsNullOrWhiteSpace(item)).ToArray());
    }
}
