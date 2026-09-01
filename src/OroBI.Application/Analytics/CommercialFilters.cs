using OroBI.Domain.Commercial;

namespace OroBI.Application.Analytics;

public static class CommercialFilters
{
    public static IEnumerable<CommercialMovement> Apply(IEnumerable<CommercialMovement> movements, CommercialFilter filter)
    {
        var query = movements;
        if (filter.StartDate is not null)
        {
            query = query.Where(movement => movement.MovementDate >= filter.StartDate.Value);
        }

        if (filter.EndDate is not null)
        {
            query = query.Where(movement => movement.MovementDate <= filter.EndDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Seller))
        {
            var seller = filter.Seller.Trim().ToUpperInvariant();
            query = query.Where(movement => movement.Seller == seller);
        }

        query = ApplyExact(query, filter.Brand, movement => movement.Brand);
        query = ApplyExact(query, filter.Group, movement => movement.Group);
        query = ApplyExact(query, filter.City, movement => movement.City);
        query = ApplyContains(query, filter.CustomerContains, movement => movement.CustomerName);
        query = ApplyContains(query, filter.ProductContains, movement => movement.ProductName);

        if (filter.MovementTypes is { Count: > 0 })
        {
            var movementTypes = filter.MovementTypes.Select(value => value.Trim().ToUpperInvariant()).ToHashSet(StringComparer.Ordinal);
            query = query.Where(movement => movementTypes.Contains(movement.MovementType));
        }

        return query;
    }

    private static IEnumerable<CommercialMovement> ApplyExact(IEnumerable<CommercialMovement> query, string? filter, Func<CommercialMovement, string> selector) =>
        string.IsNullOrWhiteSpace(filter)
            ? query
            : query.Where(movement => selector(movement) == filter.Trim().ToUpperInvariant());

    private static IEnumerable<CommercialMovement> ApplyContains(IEnumerable<CommercialMovement> query, string? filter, Func<CommercialMovement, string> selector) =>
        string.IsNullOrWhiteSpace(filter)
            ? query
            : query.Where(movement => selector(movement).Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase));
}
