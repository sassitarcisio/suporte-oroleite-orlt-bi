using System.Text;
using Microsoft.EntityFrameworkCore;
using OroBI.Application.Analytics;
using OroBI.Application.Imports;
using OroBI.Domain.Commercial;
using OroBI.Domain.Imports;
using OroBI.Infrastructure.Analytics;
using OroBI.Infrastructure.Closings;
using OroBI.Infrastructure.Imports;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Tests.Imports;

public sealed class DuplicateImportTests
{
    private const string Csv = "DATA;VENDEDOR;MARCA;GRUPO;TIPO;CIDADE;NOME;PRODUTO;VALTOTAL;QTDE;PRECOCUSTO;CODCLIENTE;NRODOCUMENTO\n01/08/2026;Ana;Nestle;Leites;Venda;Sao Paulo;Cliente A;Leite;100,00;1;10,00;123;456";

    [Fact]
    public async Task Reuploading_identical_content_under_another_name_reuses_the_existing_import()
    {
        await using var db = CreateDatabase();
        var workflow = new CsvImportWorkflow(db, new InMemoryImportFileStore());
        await using var firstContent = new MemoryStream(Encoding.UTF8.GetBytes(Csv));
        var first = await workflow.ImportAsync(new ImportSubmission(ImportFileType.Power, "power.csv", "text/csv", firstContent), CancellationToken.None);
        await using var secondContent = new MemoryStream(Encoding.UTF8.GetBytes(Csv));
        var second = await workflow.ImportAsync(new ImportSubmission(ImportFileType.Power, "renamed.csv", "text/csv", secondContent), CancellationToken.None);

        Assert.Equal(first.StoredFileUri, second.StoredFileUri);
        Assert.Equal(first.ProcessedRows, second.ProcessedRows);
        Assert.Single(await db.ImportBatches.ToListAsync());
        Assert.Equal(100m, (await db.CommercialMovements.SingleAsync()).TotalValue);
    }

    [Fact]
    public async Task Reuploading_goal_values_restores_the_previous_configuration_as_latest()
    {
        await using var db = CreateDatabase();
        var workflow = new CsvImportWorkflow(db, new InMemoryImportFileStore());
        foreach (var salary in new[] { "2000", "3000", "2000" })
        {
            var csv = $"SALARIO;{salary}\nCOMISSAO;1\nPPP;1000\nMARCA;FATURAMENTO;POSITIVACAO;TROCA;TROCA_PERCENTUAL\nNESTLE;100;50;25;2";
            await using var content = new MemoryStream(Encoding.UTF8.GetBytes(csv));
            await workflow.ImportAsync(new ImportSubmission(ImportFileType.GoalValues, "values.csv", "text/csv", content), CancellationToken.None);
        }

        var closing = await new SellerClosingQueryService(db).GetAsync("ANA", 2026, 8, CancellationToken.None);

        Assert.NotNull(closing);
        Assert.Equal(2000m, closing.Compensation.BaseSalary);
        Assert.Equal(3, await db.ImportBatches.CountAsync());
    }

    [Fact]
    public async Task Analytics_counts_one_identical_batch_but_preserves_distinct_files_and_legitimate_repeated_lines()
    {
        await using var db = CreateDatabase();
        var first = Batch("same-checksum");
        var repeated = Batch("same-checksum");
        var different = Batch("different-checksum");
        var rejected = ImportBatch.Start(ImportFileType.Power, "rejected.csv", "same-checksum");
        rejected.Reject("memory://rejected.csv", 1);
        db.AddRange(first, repeated, different, rejected);
        foreach (var batch in new[] { first, repeated })
            db.AddRange(Movement(batch.Id, 100m), Movement(batch.Id, 100m));
        db.Add(Movement(different.Id, 50m));
        await db.SaveChangesAsync();

        var result = await new DashboardQueryService(db).GetAsync(new CommercialFilter(), CancellationToken.None);

        Assert.Equal(250m, result.GrossSales);
        Assert.Equal(3, result.MovementCount);
        Assert.Equal(5, await db.CommercialMovements.CountAsync());
    }

    private static OroBiDbContext CreateDatabase() => new(new DbContextOptionsBuilder<OroBiDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static ImportBatch Batch(string checksum)
    {
        var batch = ImportBatch.Start(ImportFileType.Power, "power.csv", checksum);
        batch.Complete("memory://power.csv", 2, 0);
        return batch;
    }
    private static CommercialMovement Movement(Guid batch, decimal amount) => CommercialMovement.Create(batch, new DateOnly(2026, 8, 1), "ANA", "VENDA", amount, 1m);
}
