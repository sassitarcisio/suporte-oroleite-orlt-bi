using OroBI.Infrastructure.Persistence;
using OroBI.Domain.Commercial;

namespace OroBI.Infrastructure.Tests.Synchronization;

public sealed class SynchronizationPersistenceTests
{
    [Fact]
    public void DbContext_exposes_synchronization_checkpoints()
    {
        var property = typeof(OroBiDbContext).GetProperty("SynchronizationCheckpoints");

        Assert.NotNull(property);
    }

    [Fact]
    public void DbContext_exposes_synchronization_runs()
    {
        var property = typeof(OroBiDbContext).GetProperty("SynchronizationRuns");

        Assert.NotNull(property);
    }

    [Fact]
    public void Commercial_movement_exposes_a_source_record_key()
    {
        var property = typeof(CommercialMovement).GetProperty("SourceRecordKey");

        Assert.NotNull(property);
    }
}
