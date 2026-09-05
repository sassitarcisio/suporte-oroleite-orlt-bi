using Microsoft.AspNetCore.Identity;

namespace OroBI.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public bool IsActive { get; set; } = true;
    public string? RegistrationName { get; set; }
    public bool IsRegistrationPending { get; set; }
    public string? Seller { get; set; }

    public string? EntraObjectId { get; set; }
}
