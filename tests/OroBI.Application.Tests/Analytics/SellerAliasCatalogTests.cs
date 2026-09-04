using OroBI.Application.Analytics;

namespace OroBI.Application.Tests.Analytics;

public sealed class SellerAliasCatalogTests
{
    [Fact]
    public void Resolves_the_approved_marcel_name_to_the_imported_name()
    {
        Assert.Equal("MARCELO IVONEI DA ROSA", SellerAliasCatalog.ResolveImportedName("MARCELO DA ROSA"));
    }

    [Fact]
    public void Resolves_the_approved_rodrigo_name_to_the_imported_name()
    {
        Assert.Equal("RODRIGO", SellerAliasCatalog.ResolveImportedName("RODRIGO KEHL"));
    }
}
