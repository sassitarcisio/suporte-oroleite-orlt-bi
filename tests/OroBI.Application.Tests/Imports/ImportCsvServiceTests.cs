using OroBI.Application.Imports;
using OroBI.Domain.Imports;

namespace OroBI.Application.Tests.Imports;

public sealed class ImportCsvServiceTests
{
    [Fact]
    public void Rejects_power_file_missing_required_column()
    {
        const string csv = "DATA;VENDEDOR\n01/01/2026;ANA";

        var result = ImportCsvService.Validate(ImportFileType.Power, csv);

        Assert.Equal(ImportValidationStatus.Rejected, result.Status);
        Assert.Contains(result.Errors, error => error.Message.Contains("NRODOCUMENTO", StringComparison.Ordinal));
    }

    [Fact]
    public void Rejects_goal_values_file_missing_brand_table_header()
    {
        const string csv = "PPP;R$ 1.000,00\nSALARIO;R$ 1.951,00;COMISSAO;1%";

        var result = ImportCsvService.Validate(ImportFileType.GoalValues, csv);

        Assert.Equal(ImportValidationStatus.Rejected, result.Status);
        Assert.Contains(result.Errors, error => error.Message.Contains("MARCA", StringComparison.Ordinal));
    }
}
