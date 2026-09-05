using Microsoft.EntityFrameworkCore;
using OroBI.Application.Analytics;
using OroBI.Domain.Commercial;
using OroBI.Domain.Imports;
using OroBI.Infrastructure.Analytics;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Tests.Analytics;

public sealed class DashboardQueryServiceTests
{
    [Fact]
    public async Task Margin_details_respect_every_filter_and_duplicate_batches_while_preserving_repeated_source_rows()
    {
        var options = new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(nameof(Margin_details_respect_every_filter_and_duplicate_batches_while_preserving_repeated_source_rows))
            .Options;
        await using var db = new OroBiDbContext(options);
        foreach (var fileName in new[] { "original.csv", "copy.csv" })
        {
            var batch = ImportBatch.Start(ImportFileType.Power, fileName, "same-content");
            batch.Complete(fileName, 12, 0);
            db.ImportBatches.Add(batch);
            CommercialMovement Sale(string seller = "ANA", string brand = "MARCA", string group = "GRUPO", string city = "CIDADE", string customer = "CLIENTE ALVO", string product = "PRODUTO ALVO", int day = 10) =>
                CommercialMovement.CreateFromImport(batch.Id, new DateOnly(2026, 8, day), seller, brand, group, "VENDA", city, customer, product, 100m, 1m, 40m, "1", "1");
            db.CommercialMovements.AddRange(
                Sale(), Sale(),
                Sale(seller: "BRUNO"), Sale(brand: "OUTRA"), Sale(group: "OUTRO"),
                Sale(city: "OUTRA"), Sale(customer: "OUTRO"), Sale(product: "OUTRO"),
                Sale(day: 1), Sale(day: 31),
                CommercialMovement.CreateFromImport(batch.Id, new DateOnly(2026, 8, 10), "ANA", "MARCA", "GRUPO", "DEVOL ENT", "CIDADE", "CLIENTE ALVO", "PRODUTO ALVO", -20m, -1m, 4m, "1", "2"),
                CommercialMovement.CreateFromImport(batch.Id, new DateOnly(2026, 8, 10), "ANA", "MARCA", "GRUPO", "BONIFICACAO", "CIDADE", "CLIENTE ALVO", "PRODUTO ALVO", 100m, 1m, 40m, "1", "3"));
        }
        await db.SaveChangesAsync();
        var filter = new CommercialFilter(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 10), "ANA", "MARCA", "GRUPO", "CIDADE", "cliente alvo", "produto alvo");
        var service = new DashboardQueryService(db);

        var gross = await service.GetMarginsAsync(filter, CancellationToken.None);
        var net = await service.GetNetMarginAsync(filter, CancellationToken.None);

        Assert.Equal(200m, gross.Revenue);
        Assert.Equal(80m, gross.Cost);
        Assert.Equal(2, gross.MovementCount);
        Assert.Equal(200m, Assert.Single(gross.Groups["customer"]).Revenue);
        Assert.Equal(180m, net.NetSales);
        Assert.Equal(76m, net.NetCost);
        Assert.Equal(104m, net.LiquidProfit);
        Assert.Equal(3, net.MovementCount);
        Assert.Equal(20m, Assert.Single(net.Groups["product"]).CustomerReturns);
    }

    [Fact]
    public async Task Returns_filtered_dashboard_summary()
    {
        var options = new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(nameof(Returns_filtered_dashboard_summary))
            .Options;
        await using var db = new OroBiDbContext(options);
        var batchId = ImportBatch.Start(ImportFileType.Power, "power.csv", "abc").Id;
        db.CommercialMovements.AddRange(
            CommercialMovement.Create(batchId, new DateOnly(2026, 1, 1), "ANA", "VENDA", 100m, 1m),
            CommercialMovement.Create(batchId, new DateOnly(2026, 1, 1), "BRUNO", "VENDA", 200m, 1m));
        await db.SaveChangesAsync();

        var result = await new DashboardQueryService(db).GetAsync(new CommercialFilter(Seller: "ANA"), CancellationToken.None);

        Assert.Equal(100m, result.GrossSales);
    }

    [Fact]
    public async Task Returns_liquid_margin_after_returns_and_losses()
    {
        var options = new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(nameof(Returns_liquid_margin_after_returns_and_losses))
            .Options;
        await using var db = new OroBiDbContext(options);
        var batchId = ImportBatch.Start(ImportFileType.Power, "power.csv", "abc").Id;
        db.CommercialMovements.AddRange(
            CommercialMovement.CreateFromImport(batchId, new DateOnly(2026, 1, 1), "ANA", "MARCA", "GRUPO", "VENDA", "CIDADE", "CLIENTE", "PRODUTO", 100m, 1m, 40m, "1", "1"),
            CommercialMovement.CreateFromImport(batchId, new DateOnly(2026, 1, 2), "ANA", "MARCA", "GRUPO", "DEVOLUCAO", "CIDADE", "CLIENTE", "PRODUTO", -10m, -1m, 4m, "1", "2"),
            CommercialMovement.CreateFromImport(batchId, new DateOnly(2026, 1, 3), "ANA", "MARCA", "GRUPO", "TROCA", "CIDADE", "CLIENTE", "PRODUTO", -20m, -2m, 3m, "1", "3"),
            CommercialMovement.Create(batchId, new DateOnly(2026, 1, 4), "ANA", "DESC BOLETO", -5m, 1m));
        await db.SaveChangesAsync();

        var result = await new DashboardQueryService(db).GetNetMarginAsync(new CommercialFilter(), CancellationToken.None);

        Assert.Equal(90m, result.NetSales);
        Assert.Equal(36m, result.NetCost);
        Assert.Equal(6m, result.TradeLosses);
        Assert.Equal(43m, result.LiquidProfit);
    }
}
