namespace OroBI.Application.Tests.Synchronization;

public sealed class SynchronizationContractTests
{
    [Fact]
    public void Application_exposes_a_normalized_firebird_commercial_record_contract()
    {
        var type = Type.GetType("OroBI.Application.Synchronization.FirebirdCommercialRecord, OroBI.Application");

        Assert.NotNull(type);
    }

    [Fact]
    public void Application_exposes_a_synchronization_page_contract()
    {
        var type = Type.GetType("OroBI.Application.Synchronization.SynchronizationPage, OroBI.Application");

        Assert.NotNull(type);
    }

    [Fact]
    public void Application_exposes_a_firebird_page_reader_contract()
    {
        var type = Type.GetType("OroBI.Application.Synchronization.IFirebirdCommercialReader, OroBI.Application");

        Assert.NotNull(type);
    }
}
