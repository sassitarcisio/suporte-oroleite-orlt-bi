using OroBI.Application.Analytics;
using OroBI.Domain.Commercial;

namespace OroBI.Infrastructure.Analytics;

public static class CommercialMovementQuery
{
    public static IQueryable<CommercialMovement> ApplyFilters(IQueryable<CommercialMovement> query, CommercialFilter filter)
    {
        if (filter.StartDate is { } start) query = query.Where(item => item.MovementDate >= start);
        if (filter.EndDate is { } end) query = query.Where(item => item.MovementDate <= end);
        if (!string.IsNullOrWhiteSpace(filter.Seller))
        {
            var names = SellerAliasCatalog.GetMatchingNames(filter.Seller);
            query = query.Where(item => names.Contains(item.Seller.Trim().ToUpper()));
        }
        if (!string.IsNullOrWhiteSpace(filter.Brand))
        {
            var brand = filter.Brand.Trim().ToUpperInvariant();
            query = query.Where(item => item.Brand == brand);
        }
        if (!string.IsNullOrWhiteSpace(filter.Group))
        {
            var group = filter.Group.Trim().ToUpperInvariant();
            query = query.Where(item => item.Group == group);
        }
        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            var city = filter.City.Trim().ToUpperInvariant();
            query = query.Where(item => item.City == city);
        }
        if (!string.IsNullOrWhiteSpace(filter.CustomerContains))
        {
            var customer = filter.CustomerContains.Trim().ToUpperInvariant();
            query = query.Where(item => item.CustomerName.ToUpper().Contains(customer));
        }
        if (!string.IsNullOrWhiteSpace(filter.ProductContains))
        {
            var product = filter.ProductContains.Trim().ToUpperInvariant();
            query = query.Where(item => item.ProductName.ToUpper().Contains(product));
        }
        if (filter.MovementTypes is { Count: > 0 })
        {
            var types = filter.MovementTypes.Select(type => type.Trim().ToUpperInvariant()).ToArray();
            query = query.Where(item => types.Contains(item.MovementType));
        }
        return query;
    }
}
