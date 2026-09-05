using OroBI.Application.Analytics;
using OroBI.Domain.Commercial;
using OroBI.Domain.Imports;

namespace OroBI.Application.Tests.Analytics;

public sealed class CommercialFiltersTests
{
    [Theory]
    [InlineData("ANDERSON GONCALVES SOUZA", "VENDEDOR: ANDERSON GONCALVES SOUZA")]
    [InlineData("VENDEDOR: ANDERSON GONCALVES SOUZA", "ANDERSON GONCALVES SOUZA")]
    [InlineData("MARCELO DA ROSA", "MARCELO IVONEI DA ROSA")]
    [InlineData("RODRIGO", "RODRIGO KEHL")]
    [InlineData("DEIVID MANNES", "SUPERVISOR: DEIVID MANNES")]
    public void Matches_equivalent_seller_names_on_both_sides(string filterSeller, string storedSeller)
    {
        var rows = new[]
        {
            CommercialMovement.Create(Guid.NewGuid(), new DateOnly(2026, 8, 1), storedSeller, "VENDA", 100m, 1m),
            CommercialMovement.Create(Guid.NewGuid(), new DateOnly(2026, 8, 1), "OUTRO", "VENDA", 500m, 1m)
        };

        var result = CommercialFilters.Apply(rows, new CommercialFilter(Seller: filterSeller));

        Assert.Equal(100m, Assert.Single(result).TotalValue);
    }

    [Fact]
    public void Applies_inclusive_date_range_and_normalized_seller()
    {
        var batchId = ImportBatch.Start(ImportFileType.Power, "power.csv", "abc").Id;
        var movements = new[]
        {
            CommercialMovement.Create(batchId, new DateOnly(2026, 1, 1), "ANA", "VENDA", 10m, 1m),
            CommercialMovement.Create(batchId, new DateOnly(2026, 1, 31), "ANA", "VENDA", 20m, 1m),
            CommercialMovement.Create(batchId, new DateOnly(2026, 2, 1), "ANA", "VENDA", 30m, 1m),
            CommercialMovement.Create(batchId, new DateOnly(2026, 1, 1), "BRUNO", "VENDA", 40m, 1m)
        };

        var result = CommercialFilters.Apply(movements, new CommercialFilter(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), "ana"));

        Assert.Equal(2, result.Count());
        Assert.Equal(30m, result.Sum(movement => movement.TotalValue));
    }

    [Fact]
    public void Applies_brand_and_movement_type_filters()
    {
        var batchId = ImportBatch.Start(ImportFileType.Power, "power.csv", "abc").Id;
        var sale = CommercialMovement.CreateFromImport(batchId, new DateOnly(2026, 1, 1), "ANA", "NESTLE", "LEITES", "VENDA", "SAO PAULO", "Cliente", "Leite", 10m, 1m, 1m, "1", "1");
        var trade = CommercialMovement.CreateFromImport(batchId, new DateOnly(2026, 1, 1), "ANA", "NESTLE", "LEITES", "TROCA", "SAO PAULO", "Cliente", "Leite", -2m, 1m, 1m, "1", "2");
        var otherBrand = CommercialMovement.CreateFromImport(batchId, new DateOnly(2026, 1, 1), "ANA", "ZINHO", "LEITES", "VENDA", "SAO PAULO", "Cliente", "Leite", 20m, 1m, 1m, "1", "3");

        var result = CommercialFilters.Apply([sale, trade, otherBrand], new CommercialFilter(Brand: "nestle", MovementTypes: ["venda"]));

        Assert.Single(result);
        Assert.Equal(10m, result.Single().TotalValue);
    }
}
