using Microsoft.EntityFrameworkCore;
using OroBI.Domain.Closings;
using OroBI.Domain.Sellers;

namespace OroBI.Infrastructure.Persistence;

public sealed partial class OroBiDbContext
{
    public DbSet<ClosingSnapshot> ClosingSnapshots => Set<ClosingSnapshot>();

    partial void ConfigurePortalModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClosingSnapshot>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.SellerId, item.Year, item.Month }).IsUnique();
            entity.HasOne<Seller>().WithMany().HasForeignKey(item => item.SellerId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.SnapshotJson).HasColumnType("jsonb");
            entity.Property(item => item.ReviewedBy).HasMaxLength(450);
            entity.Property(item => item.ApprovedBy).HasMaxLength(450);
            entity.Property(item => item.Revision).IsConcurrencyToken();
        });
    }
}
