using OroBI.Domain.Sellers;

namespace OroBI.Infrastructure.Identity;

public sealed class UserSellerAccess
{
    public string UserId { get; set; } = string.Empty;
    public Guid SellerId { get; set; }
    public bool IsActive { get; set; } = true;
    public SellerPortalPermissions Permissions { get; set; } = new();
    public Seller Seller { get; set; } = null!;
}

public sealed class AccountAuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string ActorUserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string? DetailsJson { get; set; }
}
