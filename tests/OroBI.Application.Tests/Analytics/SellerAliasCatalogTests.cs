using OroBI.Application.Analytics;

namespace OroBI.Application.Tests.Analytics;

public sealed class SellerAliasCatalogTests
{
    [Fact]
    public void Resolves_the_approved_marcel_name_to_the_imported_name()
    {
        Assert.Equal("VENDEDOR: MARCELO IVONEI DA ROSA", SellerAliasCatalog.ResolveImportedName("MARCELO DA ROSA"));
    }

    [Theory]
    [InlineData("MARCIO FERNANDES", "VENDEDOR: MARCIO FERNANDES")]
    [InlineData("MARCIO LUIZ DA ROSA", "VENDEDOR: MARCIO LUIZ DA ROSA")]
    [InlineData("ANDERSON GONCALVES SOUZA", "VENDEDOR: ANDERSON GONCALVES SOUZA")]
    [InlineData("DEIVID MANNES", "SUPERVISOR: DEIVID MANNES")]
    [InlineData("RODRIGO KEHL", "VENDEDOR: RODRIGO")]
    [InlineData("MARCELO DA ROSA", "VENDEDOR: MARCELO IVONEI DA ROSA")]
    [InlineData("PAULO RICARDO LOPES", "VENDEDOR: PAULO RICARDO LOPES")]
    [InlineData("RAMON DO NASCIMENTO", "VENDEDOR: RAMON DO NASCIMENTO")]
    [InlineData("TIAGO MARTINS", "VENDEDOR: TIAGO MARTINS")]
    public void Resolves_each_approved_catalog_name_to_the_imported_name(string seller, string importedSeller)
    {
        Assert.Equal(importedSeller, SellerAliasCatalog.ResolveImportedName(seller));
    }
}
