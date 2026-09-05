namespace OroBI.Domain.Sellers;

public sealed class Seller
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ImportedName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed record SellerPortalPermissions
{
    public bool CanViewRevenue { get; init; } = true;
    public bool CanViewCommission { get; init; } = true;
    public bool CanViewPrize { get; init; } = true;
    public bool CanViewPPP { get; init; } = true;
    public bool CanViewGoals { get; init; } = true;
    public bool CanViewTrades { get; init; } = true;
    public bool CanViewCustomers { get; init; } = true;
}
