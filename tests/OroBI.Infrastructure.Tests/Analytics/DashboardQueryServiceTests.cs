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
            CommercialMovement.Create(batchId, new DateOnly(2026, 1, 2), "ANA", "DEVOLUCAO", -10m, 1m),
            CommercialMovement.Create(batchId, new DateOnly(2026, 1, 3), "ANA", "TROCA", -20m, 1m),
            CommercialMovement.Create(batchId, new DateOnly(2026, 1, 4), "ANA", "DESC BOLETO", -5m, 1m));
        await db.SaveChangesAsync();

        var result = await new DashboardQueryService(db).GetNetMarginAsync(new CommercialFilter(), CancellationToken.None);

        Assert.Equal(90m, result.NetSales);
        Assert.Equal(25m, result.LiquidProfit);
    }
}
