namespace OroBI.Application.Analytics;

public sealed record CommercialFilter(
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    string? Seller = null,
    string? Brand = null,
    string? Group = null,
    string? City = null,
    string? CustomerContains = null,
    string? ProductContains = null,
    IReadOnlyCollection<string>? MovementTypes = null);
