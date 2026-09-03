using OroBI.Api.Analytics;

namespace OroBI.Api.IntegrationTests.Analytics;

public sealed class SellerCatalogTests
{
    [Fact]
    public void Contains_only_the_official_sellers()
    {
        Assert.Equal(
        [
            "MARCIO FERNANDES",
            "MARCIO LUIZ DA ROSA",
            "ANDERSON GONCALVES SOUZA",
            "DEIVID MANNES",
            "RODRIGO KEHL",
            "MARCELO DA ROSA",
            "PAULO RICARDO LOPES",
            "ELTON CONSTANTE",
            "TIAGO MARTINS"
        ], SellerCatalog.Names);
    }
}
