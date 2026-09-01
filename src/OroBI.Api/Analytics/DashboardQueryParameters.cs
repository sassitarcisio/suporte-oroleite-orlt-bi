using OroBI.Application.Analytics;

namespace OroBI.Api.Analytics;

public sealed record DashboardQueryParameters(
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    string? Seller = null,
    string? Brand = null,
    string? Group = null,
    string? City = null,
    string? CustomerContains = null,
    string? ProductContains = null,
    string[]? MovementTypes = null)
{
    public CommercialFilter ToCommercialFilter() => new(
        StartDate,
        EndDate,
        Seller,
        Brand,
        Group,
        City,
        CustomerContains,
        ProductContains,
        MovementTypes);
}
