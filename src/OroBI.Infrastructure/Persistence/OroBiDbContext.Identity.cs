using Microsoft.EntityFrameworkCore;
using OroBI.Domain.Sellers;
using OroBI.Infrastructure.Identity;

namespace OroBI.Infrastructure.Persistence;

public sealed partial class OroBiDbContext
{
    public DbSet<Seller> Sellers => Set<Seller>();
    public DbSet<UserSellerAccess> UserSellerAccesses => Set<UserSellerAccess>();
    public DbSet<AccountAuditEvent> AccountAuditEvents => Set<AccountAuditEvent>();

    partial void ConfigureIdentityModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>().Property(user => user.IsActive).HasDefaultValue(true);
        modelBuilder.Entity<ApplicationUser>().Property(user => user.RegistrationName).HasMaxLength(120);
        modelBuilder.Entity<ApplicationUser>().Property(user => user.IsRegistrationPending).HasDefaultValue(false);
        modelBuilder.Entity<Seller>(entity =>
        {
            entity.HasKey(seller => seller.Id);
            entity.Property(seller => seller.Name).HasMaxLength(120);
            entity.Property(seller => seller.ImportedName).HasMaxLength(120);
            entity.HasIndex(seller => seller.ImportedName).IsUnique();
        });
        modelBuilder.Entity<UserSellerAccess>(entity =>
        {
            entity.HasKey(access => new { access.UserId, access.SellerId });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(access => access.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(access => access.Seller).WithMany().HasForeignKey(access => access.SellerId).OnDelete(DeleteBehavior.Restrict);
            entity.OwnsOne(access => access.Permissions);
            entity.Navigation(access => access.Permissions).IsRequired();
        });
        modelBuilder.Entity<AccountAuditEvent>(entity =>
        {
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.ActorUserId).HasMaxLength(450);
            entity.Property(audit => audit.TargetId).HasMaxLength(450);
            entity.Property(audit => audit.Action).HasMaxLength(80);
            entity.HasIndex(audit => audit.OccurredAtUtc);
        });
    }
}
