using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OroBI.Domain.Commercial;
using OroBI.Domain.Closings;
using OroBI.Domain.Goals;
using OroBI.Domain.Imports;
using OroBI.Domain.Ppp;
using OroBI.Domain.Synchronization;
using OroBI.Infrastructure.Identity;

namespace OroBI.Infrastructure.Persistence;

public sealed class OroBiDbContext(DbContextOptions<OroBiDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string>(options)
{
    public DbSet<CommercialMovement> CommercialMovements => Set<CommercialMovement>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportError> ImportErrors => Set<ImportError>();
    public DbSet<GoalRecord> GoalRecords => Set<GoalRecord>();
    public DbSet<GoalValueRecord> GoalValueRecords => Set<GoalValueRecord>();
    public DbSet<PppRecord> PppRecords => Set<PppRecord>();
    public DbSet<SellerClosingConfiguration> SellerClosingConfigurations => Set<SellerClosingConfiguration>();
    public DbSet<SynchronizationCheckpoint> SynchronizationCheckpoints => Set<SynchronizationCheckpoint>();
    public DbSet<SynchronizationRun> SynchronizationRuns => Set<SynchronizationRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.Seller).HasMaxLength(120);
            entity.Property(user => user.EntraObjectId).HasMaxLength(64);
            entity.HasIndex(user => user.EntraObjectId).IsUnique();
        });

        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.HasKey(batch => batch.Id);
            entity.Property(batch => batch.FileName).HasMaxLength(260);
            entity.Property(batch => batch.Checksum).HasMaxLength(64);
        });

        modelBuilder.Entity<CommercialMovement>(entity =>
        {
            entity.HasKey(movement => movement.Id);
            entity.Property(movement => movement.TotalValue).HasPrecision(18, 2);
            entity.Property(movement => movement.Quantity).HasPrecision(18, 4);
            entity.Property(movement => movement.UnitCost).HasPrecision(18, 4);
            entity.HasIndex(movement => new { movement.MovementDate, movement.Seller });
            entity.HasIndex(movement => new { movement.MovementDate, movement.Brand });
            entity.HasIndex(movement => movement.ImportBatchId);
            entity.HasIndex(movement => movement.CustomerCode);
            entity.HasIndex(movement => new { movement.SourceSystem, movement.SourceRecordKey }).IsUnique();
        });

        modelBuilder.Entity<SellerClosingConfiguration>(entity =>
        {
            entity.HasKey(configuration => configuration.Id);
            entity.Property(configuration => configuration.Seller).HasMaxLength(120);
            entity.Property(configuration => configuration.BaseSalary).HasPrecision(18, 2);
            entity.Property(configuration => configuration.CommissionPercent).HasPrecision(9, 4);
            entity.Property(configuration => configuration.PppMaximumAward).HasPrecision(18, 2);
            entity.HasIndex(configuration => new { configuration.Seller, configuration.Year, configuration.Month }).IsUnique();
        });

        modelBuilder.Entity<SynchronizationCheckpoint>(entity =>
        {
            entity.HasKey(checkpoint => checkpoint.SourceSystem);
            entity.Property(checkpoint => checkpoint.SourceSystem).HasMaxLength(64);
            entity.Property(checkpoint => checkpoint.Watermark).HasMaxLength(256);
        });

        modelBuilder.Entity<SynchronizationRun>(entity =>
        {
            entity.HasKey(run => run.Id);
            entity.Property(run => run.SourceSystem).HasMaxLength(64);
            entity.Property(run => run.ErrorSummary).HasMaxLength(2048);
            entity.HasIndex(run => new { run.SourceSystem, run.StartedAtUtc });
        });
    }
}
