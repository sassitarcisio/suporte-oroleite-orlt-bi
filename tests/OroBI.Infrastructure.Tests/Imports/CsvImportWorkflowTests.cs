using Microsoft.EntityFrameworkCore;
using OroBI.Application.Imports;
using OroBI.Domain.Imports;
using OroBI.Infrastructure.Imports;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Tests.Imports;

public sealed class CsvImportWorkflowTests
{
    [Fact]
    public async Task Persists_completed_batch_for_valid_power_headers()
    {
        var options = new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(nameof(Persists_completed_batch_for_valid_power_headers))
            .Options;
        await using var db = new OroBiDbContext(options);
        var fileStore = new InMemoryImportFileStore();
        var workflow = new CsvImportWorkflow(db, fileStore);
        const string csv = "DATA;VENDEDOR;MARCA;GRUPO;TIPO;CIDADE;NOME;PRODUTO;VALTOTAL;QTDE;PRECOCUSTO;CODCLIENTE;NRODOCUMENTO\n";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        var result = await workflow.ImportAsync(new ImportSubmission(ImportFileType.Power, "power.csv", "text/csv", stream), CancellationToken.None);

        Assert.Equal(ImportBatchStatus.Completed, result.Status);
        Assert.NotNull(result.StoredFileUri);
        Assert.Single(db.ImportBatches);
    }

    [Fact]
    public async Task Persists_normalized_power_movement()
    {
        var options = new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(nameof(Persists_normalized_power_movement))
            .Options;
        await using var db = new OroBiDbContext(options);
        var workflow = new CsvImportWorkflow(db, new InMemoryImportFileStore());
        const string csv = "DATA;VENDEDOR;MARCA;GRUPO;TIPO;CIDADE;NOME;PRODUTO;VALTOTAL;QTDE;PRECOCUSTO;CODCLIENTE;NRODOCUMENTO\n01/08/2026;Ana;Nestle;Leites;Venda;Sao Paulo;Cliente A;Leite;1.250,50;2,0000;10,2500;123;456";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        await workflow.ImportAsync(new ImportSubmission(ImportFileType.Power, "power.csv", "text/csv", stream), CancellationToken.None);

        var movement = await db.CommercialMovements.SingleAsync();
        Assert.Equal(new DateOnly(2026, 8, 1), movement.MovementDate);
        Assert.Equal("ANA", movement.Seller);
        Assert.Equal("VENDA", movement.MovementType);
        Assert.Equal(1250.50m, movement.TotalValue);
        Assert.Equal(2m, movement.Quantity);
        Assert.Equal(10.2500m, movement.UnitCost);
    }

    [Fact]
    public async Task Imports_power_csv_encoded_as_windows_1252()
    {
        var options = new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(nameof(Imports_power_csv_encoded_as_windows_1252))
            .Options;
        await using var db = new OroBiDbContext(options);
        var workflow = new CsvImportWorkflow(db, new InMemoryImportFileStore());
        const string beforeAccent = "DATA;VENDEDOR;MARCA;GRUPO;TIPO;CIDADE;NOME;PRODUTO;VALTOTAL;QTDE;PRECOCUSTO;CODCLIENTE;NRODOCUMENTO\n01/08/2026;Ana;Nestle;Leites;Venda;Sao Paulo;Jos";
        const string afterAccent = ";Leite;100,00;1;10,00;123;456";
        var bytes = System.Text.Encoding.ASCII.GetBytes(beforeAccent)
            .Concat([(byte)0xE9])
            .Concat(System.Text.Encoding.ASCII.GetBytes(afterAccent))
            .ToArray();
        await using var stream = new MemoryStream(bytes);

        var result = await workflow.ImportAsync(new ImportSubmission(ImportFileType.Power, "power.csv", "text/csv", stream), CancellationToken.None);

        Assert.Equal(ImportBatchStatus.Completed, result.Status);
        Assert.Equal("Jos\u00e9", (await db.CommercialMovements.SingleAsync()).CustomerName);
    }

    [Fact]
    public async Task Accepts_legacy_power_header_and_maps_network_to_group()
    {
        var options = new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(nameof(Accepts_legacy_power_header_and_maps_network_to_group))
            .Options;
        await using var db = new OroBiDbContext(options);
        var workflow = new CsvImportWorkflow(db, new InMemoryImportFileStore());
        const string csv = "CODCLIENTE;NOME;CODREDE;REDE;CIDADE;UF;DATA;NRODOCUMENTO;VENDEDOR;CODPRODUTO;PRODUTO;MARCA;QTDE;PRECO;PRECOCUSTO;VALTOTAL;TIPO;\n123;Cliente A;28;Atacadao;Sao Paulo;SP;01/08/2026;456;Ana;789;Leite;Nestle;2;10,00;8,00;20,00;Venda;";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        var result = await workflow.ImportAsync(new ImportSubmission(ImportFileType.Power, "power.csv", "text/csv", stream), CancellationToken.None);

        Assert.Equal(ImportBatchStatus.Completed, result.Status);
        var movement = await db.CommercialMovements.SingleAsync();
        Assert.Equal("ATACADAO", movement.Group);
    }

    [Fact]
    public async Task Defaults_blank_quantity_to_zero_for_legacy_discount_rows()
    {
        var options = new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(nameof(Defaults_blank_quantity_to_zero_for_legacy_discount_rows))
            .Options;
        await using var db = new OroBiDbContext(options);
        var workflow = new CsvImportWorkflow(db, new InMemoryImportFileStore());
        const string csv = "DATA;VENDEDOR;MARCA;GRUPO;TIPO;CIDADE;NOME;PRODUTO;VALTOTAL;QTDE;PRECOCUSTO;CODCLIENTE;NRODOCUMENTO\n01/08/2026;Operacao Bauducco;;;DESC BOLETO;Sao Paulo;Cliente A;;-290,87;;;123;456";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        var result = await workflow.ImportAsync(new ImportSubmission(ImportFileType.Power, "power.csv", "text/csv", stream), CancellationToken.None);

        Assert.Equal(ImportBatchStatus.Completed, result.Status);
        var movement = await db.CommercialMovements.SingleAsync();
        Assert.Equal(0m, movement.Quantity);
        Assert.Equal(0m, movement.UnitCost);
        Assert.Equal(-290.87m, movement.TotalValue);
    }

    [Fact]
    public async Task Keeps_valid_rows_when_a_power_row_is_invalid()
    {
        var options = new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(nameof(Keeps_valid_rows_when_a_power_row_is_invalid))
            .Options;
        await using var db = new OroBiDbContext(options);
        var workflow = new CsvImportWorkflow(db, new InMemoryImportFileStore());
        const string csv = "DATA;VENDEDOR;MARCA;GRUPO;TIPO;CIDADE;NOME;PRODUTO;VALTOTAL;QTDE;PRECOCUSTO;CODCLIENTE;NRODOCUMENTO\n01/08/2026;Ana;Nestle;Leites;Venda;Sao Paulo;Cliente A;Leite;100,00;1;10,00;123;456\ndata-invalida;Bruno;Nestle;Leites;Venda;Sao Paulo;Cliente B;Leite;200,00;1;10,00;124;457";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        var result = await workflow.ImportAsync(new ImportSubmission(ImportFileType.Power, "power.csv", "text/csv", stream), CancellationToken.None);

        Assert.Equal(ImportBatchStatus.CompletedWithErrors, result.Status);
        Assert.Equal(1, result.ProcessedRows);
        Assert.Equal(1, result.ErrorRows);
        Assert.Single(db.CommercialMovements);
        var error = await db.ImportErrors.SingleAsync();
        Assert.Equal(3, error.LineNumber);
        Assert.Contains("DATA", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Persists_ppp_record_from_valid_file()
    {
        var options = new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(nameof(Persists_ppp_record_from_valid_file))
            .Options;
        await using var db = new OroBiDbContext(options);
        var workflow = new CsvImportWorkflow(db, new InMemoryImportFileStore());
        const string csv = "ANO;MES;VENDEDOR;SEGMENTO;QTDE_CLIENTES;QTDE_ITENS_SEGMENTO;GRUPOS_COLOCADOS\n2026;8;Ana;Leites;10;2;15";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        var result = await workflow.ImportAsync(new ImportSubmission(ImportFileType.Ppp, "ppp.csv", "text/csv", stream), CancellationToken.None);

        Assert.Equal(ImportBatchStatus.Completed, result.Status);
        Assert.Equal(1, result.ProcessedRows);
        var record = await db.PppRecords.SingleAsync();
        Assert.Equal("ANA", record.Seller);
        Assert.Equal(15, record.GroupsPlaced);
    }

    [Fact]
    public async Task Persists_goal_record_from_valid_file()
    {
        var options = new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(nameof(Persists_goal_record_from_valid_file))
            .Options;
        await using var db = new OroBiDbContext(options);
        var workflow = new CsvImportWorkflow(db, new InMemoryImportFileStore());
        const string csv = "VENDEDOR;MES;ANO;TIPOMETA;DESCRICAO;META;ALCANCADO\nAna;8;2026;FATURAMENTO;Meta mensal;1000,00;950,00";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        var result = await workflow.ImportAsync(new ImportSubmission(ImportFileType.Goals, "metas.csv", "text/csv", stream), CancellationToken.None);

        Assert.Equal(ImportBatchStatus.Completed, result.Status);
        var record = await db.GoalRecords.SingleAsync();
        Assert.Equal("ANA", record.Seller);
        Assert.Equal(1000m, record.Target);
        Assert.Equal(950m, record.Achieved);
    }

    [Fact]
    public async Task Classifies_legacy_goal_type_from_description()
    {
        var options = new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(nameof(Classifies_legacy_goal_type_from_description))
            .Options;
        await using var db = new OroBiDbContext(options);
        var workflow = new CsvImportWorkflow(db, new InMemoryImportFileStore());
        const string csv = "VENDEDOR;MES;ANO;TIPOMETA;DESCRICAO;META;ALCANCADO\nAna;8;2026;18;Marca NESTLE / Valor;1000,00;950,00";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        await workflow.ImportAsync(new ImportSubmission(ImportFileType.Goals, "metas.csv", "text/csv", stream), CancellationToken.None);

        Assert.Equal("FATURAMENTO", (await db.GoalRecords.SingleAsync()).GoalType);
    }

    [Fact]
    public async Task Persists_goal_value_record_from_brand_table()
    {
        var options = new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(nameof(Persists_goal_value_record_from_brand_table))
            .Options;
        await using var db = new OroBiDbContext(options);
        var workflow = new CsvImportWorkflow(db, new InMemoryImportFileStore());
        const string csv = "MARCA;FATURAMENTO;POSITIVACAO;TROCA;TROCA_PERCENTUAL\nOroleite;1000,00;300,00;200,00;5,5";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        var result = await workflow.ImportAsync(new ImportSubmission(ImportFileType.GoalValues, "valor-metas.csv", "text/csv", stream), CancellationToken.None);

        Assert.Equal(ImportBatchStatus.Completed, result.Status);
        var record = await db.GoalValueRecords.SingleAsync();
        Assert.Equal("OROLEITE", record.Brand);
        Assert.Equal(1000m, record.RevenuePrize);
        Assert.Equal(5.5m, record.TradePercentageGoal);
    }

    [Fact]
    public async Task Persists_closing_defaults_and_seller_salary_from_goal_values_file()
    {
        var options = new DbContextOptionsBuilder<OroBiDbContext>()
            .UseInMemoryDatabase(nameof(Persists_closing_defaults_and_seller_salary_from_goal_values_file))
            .Options;
        await using var db = new OroBiDbContext(options);
        var workflow = new CsvImportWorkflow(db, new InMemoryImportFileStore());
        const string csv = "SALARIO;1951,00\nCOMISSAO;1\nPPP;1200,00\nVENDEDOR: ANA;2200,00\nMARCA;FATURAMENTO;POSITIVACAO;TROCA;TROCA_PERCENTUAL\nOROLEITE;1000,00;300,00;200,00;5,5";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        var result = await workflow.ImportAsync(new ImportSubmission(ImportFileType.GoalValues, "valor-metas.csv", "text/csv", stream), CancellationToken.None);

        Assert.Equal(ImportBatchStatus.Completed, result.Status);
        var defaults = await db.ImportedClosingDefaults.SingleAsync();
        Assert.Equal(1951m, defaults.BaseSalary);
        Assert.Equal(1m, defaults.CommissionPercent);
        Assert.Equal(1200m, defaults.PppMaximumAward);
        Assert.Equal(2200m, defaults.SellerSalaries["VENDEDOR: ANA"]);
    }
}
