using Microsoft.AspNetCore.Identity;

namespace OroBI.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public string? Seller { get; set; }

    public string? EntraObjectId { get; set; }
}
