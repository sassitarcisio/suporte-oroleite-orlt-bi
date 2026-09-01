using OroBI.Application.Analytics;
using OroBI.Domain.Commercial;
using OroBI.Domain.Imports;

namespace OroBI.Application.Tests.Analytics;

public sealed class CommercialFiltersTests
{
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
