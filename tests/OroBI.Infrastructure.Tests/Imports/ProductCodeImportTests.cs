using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OroBI.Application.Imports;
using OroBI.Domain.Commercial;
using OroBI.Domain.Imports;
using OroBI.Infrastructure.Imports;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Tests.Imports;

public sealed class ProductCodeImportTests
{
    private const string Header = "DATA;VENDEDOR;MARCA;REDE;TIPO;CIDADE;NOME;PRODUTO;VALTOTAL;QTDE;PRECOCUSTO;CODCLIENTE;NRODOCUMENTO";
    private const string Row = "01/08/2026;Ana;Nestle;Leites;Venda;Sao Paulo;Cliente A;Leite;100,00;1;10,00;123;456";

    [Theory]
    [InlineData(";CODPRODUTO", "; 00123 ", "00123")]
    [InlineData(";CODPRODUTO", ";", "")]
    [InlineData(";CODPRODUTO", "", "")]
    [InlineData("", "", "")]
    public async Task Imports_optional_product_code_as_trimmed_text(string extraHeader, string extraValue, string expected)
    {
        await using var db = CreateDatabase();
        var result = await Upload(db, Header + extraHeader + "\n" + Row + extraValue);
        db.ChangeTracker.Clear();

        Assert.Equal(ImportBatchStatus.Completed, result.Status);
        Assert.Equal(expected, Code(await db.CommercialMovements.SingleAsync()));
    }

    [Fact]
    public async Task Reupload_fills_missing_codes_without_adding_batches_or_changing_movements()
    {
        await using var db = CreateDatabase();
        var csv = Header + ";CODPRODUTO\n" + Row + ";00123\n" + Row + ";00123";
        var batch = SeedBatch(db, csv, 2);
        var first = LegacyMovement(batch.Id);
        var second = LegacyMovement(batch.Id);
        db.AddRange(first, second);
        await db.SaveChangesAsync();
        var originalIds = new[] { first.Id, second.Id }.Order().ToArray();
        db.ChangeTracker.Clear();

        var result = await Upload(db, csv);
        await Upload(db, csv);
        db.ChangeTracker.Clear();

        Assert.Equal("memory://original.csv", result.StoredFileUri);
        Assert.Equal(2, result.ProcessedRows);
        Assert.Single(await db.ImportBatches.ToListAsync());
        var movements = await db.CommercialMovements.ToListAsync();
        Assert.Equal(originalIds, movements.Select(movement => movement.Id).Order().ToArray());
        Assert.All(movements, movement => Assert.Equal("00123", Code(movement)));
        Assert.Equal(200m, movements.Sum(movement => movement.TotalValue));
        Assert.Equal(2m, movements.Sum(movement => movement.Quantity));
        Assert.All(movements, movement => Assert.Equal(10m, movement.UnitCost));
    }

    [Theory]
    [InlineData("00999")]
    [InlineData("")]
    public async Task Reupload_does_not_guess_when_identical_legacy_rows_have_different_codes(string otherCode)
    {
        await using var db = CreateDatabase();
        var csv = Header + ";CODPRODUTO\n" + Row + ";00123\n" + Row + ";" + otherCode;
        var batch = SeedBatch(db, csv, 2);
        db.AddRange(LegacyMovement(batch.Id), LegacyMovement(batch.Id));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await Upload(db, csv);
        db.ChangeTracker.Clear();

        Assert.Equal(2, await db.CommercialMovements.CountAsync());
        Assert.All(await db.CommercialMovements.ToListAsync(), movement => Assert.Equal("", Code(movement)));
    }

    [Fact]
    public async Task Reupload_only_fills_exact_matches_from_the_original_batch()
    {
        await using var db = CreateDatabase();
        var csv = Header + ";CODPRODUTO\n" + Row + ";00123";
        var batch = SeedBatch(db, csv, 1);
        var exact = LegacyMovement(batch.Id);
        var changedAmount = LegacyMovement(batch.Id, totalValue: 200m);
        var otherBatch = LegacyMovement(Guid.NewGuid());
        db.AddRange(exact, changedAmount, otherBatch);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await Upload(db, csv);
        db.ChangeTracker.Clear();

        Assert.Equal("00123", Code(await db.CommercialMovements.SingleAsync(movement => movement.Id == exact.Id)));
        Assert.Equal("", Code(await db.CommercialMovements.SingleAsync(movement => movement.Id == changedAmount.Id)));
        Assert.Equal("", Code(await db.CommercialMovements.SingleAsync(movement => movement.Id == otherBatch.Id)));
        Assert.Equal(400m, await db.CommercialMovements.SumAsync(movement => movement.TotalValue));
    }

    [Fact]
    public async Task Reupload_keeps_existing_codes_even_when_the_original_csv_differs()
    {
        await using var db = CreateDatabase();
        var csv = Header + ";CODPRODUTO\n" + Row + ";00123";
        var batch = SeedBatch(db, csv, 1);
        db.Add(CommercialMovement.CreateFromImport(batch.Id, new DateOnly(2026, 8, 1),
            "ANA", "NESTLE", "LEITES", "VENDA", "SAO PAULO", "Cliente A", "Leite", 100m, 1m, 10m, "123", "456", "existing-code"));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await Upload(db, csv);
        db.ChangeTracker.Clear();

        Assert.Equal("existing-code", (await db.CommercialMovements.SingleAsync()).ProductCode);
        Assert.Single(await db.ImportBatches.ToListAsync());
    }

    [Fact]
    public async Task Reupload_treats_a_missing_optional_cell_as_an_ambiguous_blank_code()
    {
        await using var db = CreateDatabase();
        var csv = Header + ";CODPRODUTO\n" + Row + ";00123\n" + Row;
        var batch = SeedBatch(db, csv, 2);
        db.AddRange(LegacyMovement(batch.Id), LegacyMovement(batch.Id));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await Upload(db, csv);
        db.ChangeTracker.Clear();

        Assert.All(await db.CommercialMovements.ToListAsync(), movement => Assert.Equal("", movement.ProductCode));
    }

    [Theory]
    [InlineData(nameof(CommercialMovement.MovementDate))]
    [InlineData(nameof(CommercialMovement.Seller))]
    [InlineData(nameof(CommercialMovement.Brand))]
    [InlineData(nameof(CommercialMovement.Group))]
    [InlineData(nameof(CommercialMovement.MovementType))]
    [InlineData(nameof(CommercialMovement.City))]
    [InlineData(nameof(CommercialMovement.CustomerName))]
    [InlineData(nameof(CommercialMovement.ProductName))]
    [InlineData(nameof(CommercialMovement.Quantity))]
    [InlineData(nameof(CommercialMovement.UnitCost))]
    [InlineData(nameof(CommercialMovement.CustomerCode))]
    [InlineData(nameof(CommercialMovement.DocumentNumber))]
    public async Task Reupload_requires_every_legacy_field_to_match(string changedField)
    {
        await using var db = CreateDatabase();
        var csv = Header + ";CODPRODUTO\n" + Row + ";00123";
        var batch = SeedBatch(db, csv, 1);
        var movement = LegacyMovement(batch.Id);
        db.Add(movement);
        var property = db.Entry(movement).Property(changedField);
        property.CurrentValue = property.CurrentValue switch
        {
            DateOnly date => date.AddDays(1),
            decimal value => value + 1m,
            string value => value + " changed",
            _ => throw new InvalidOperationException()
        };
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await Upload(db, csv);
        db.ChangeTracker.Clear();

        Assert.Equal("", (await db.CommercialMovements.SingleAsync()).ProductCode);
    }

    private static string Code(CommercialMovement movement) => movement.ProductCode;

    [Theory]
    [InlineData(8, "100,004")]
    [InlineData(9, "1,00004")]
    [InlineData(10, "10,00004")]
    public async Task Reupload_matches_values_at_the_precision_stored_in_the_database(int column, string rawValue)
    {
        await using var db = CreateDatabase();
        var values = Row.Split(';');
        values[column] = rawValue;
        var csv = Header + ";CODPRODUTO\n" + string.Join(';', values) + ";00123";
        var batch = SeedBatch(db, csv, 1);
        db.Add(LegacyMovement(batch.Id));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await Upload(db, csv);
        db.ChangeTracker.Clear();

        Assert.Equal("00123", (await db.CommercialMovements.SingleAsync()).ProductCode);
    }

    [Theory]
    [InlineData(8, "100,004")]
    [InlineData(9, "1,00004")]
    [InlineData(10, "10,00004")]
    public async Task Reupload_does_not_guess_when_rounding_makes_distinct_rows_identical(int column, string rawValue)
    {
        await using var db = CreateDatabase();
        var values = Row.Split(';');
        values[column] = rawValue;
        var csv = Header + ";CODPRODUTO\n" + Row + ";00123\n" + string.Join(';', values) + ";00999";
        var batch = SeedBatch(db, csv, 2);
        db.AddRange(LegacyMovement(batch.Id), LegacyMovement(batch.Id));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await Upload(db, csv);
        db.ChangeTracker.Clear();

        Assert.All(await db.CommercialMovements.ToListAsync(), movement => Assert.Equal("", movement.ProductCode));
    }

    [Theory]
    [InlineData("100,005", "100.01")]
    [InlineData("-100,005", "-100.01")]
    public async Task Reupload_matches_postgresql_rounding_away_from_zero_at_midpoints(string rawValue, string storedValue)
    {
        await using var db = CreateDatabase();
        var csv = Header + ";CODPRODUTO\n" + Row.Replace("100,00", rawValue, StringComparison.Ordinal) + ";00123";
        var batch = SeedBatch(db, csv, 1);
        db.Add(LegacyMovement(batch.Id, decimal.Parse(storedValue, System.Globalization.CultureInfo.InvariantCulture)));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await Upload(db, csv);
        db.ChangeTracker.Clear();

        Assert.Equal("00123", (await db.CommercialMovements.SingleAsync()).ProductCode);
    }

    private static OroBiDbContext CreateDatabase() => new(new DbContextOptionsBuilder<OroBiDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ImportBatch SeedBatch(OroBiDbContext db, string csv, int rows)
    {
        var batch = ImportBatch.Start(ImportFileType.Power, "original.csv", Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(csv))));
        batch.Complete("memory://original.csv", rows, 0);
        db.Add(batch);
        return batch;
    }

    private static CommercialMovement LegacyMovement(Guid batchId, decimal totalValue = 100m) => CommercialMovement.CreateFromImport(
        batchId, new DateOnly(2026, 8, 1), "ANA", "NESTLE", "LEITES", "VENDA", "SAO PAULO", "Cliente A", "Leite",
        totalValue, 1m, 10m, "123", "456");

    private static async Task<ImportExecutionResult> Upload(OroBiDbContext db, string csv)
    {
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await new CsvImportWorkflow(db, new InMemoryImportFileStore()).ImportAsync(
            new ImportSubmission(ImportFileType.Power, "renamed.csv", "text/csv", content), CancellationToken.None);
    }
}
