using OroBI.Domain.Imports;

namespace OroBI.Application.Imports;

public static class ImportCsvService
{
    private static readonly IReadOnlyDictionary<ImportFileType, string[]> RequiredHeaders =
        new Dictionary<ImportFileType, string[]>
        {
            [ImportFileType.Power] =
            [
                "DATA", "VENDEDOR", "MARCA", "TIPO", "CIDADE", "NOME", "PRODUTO",
                "VALTOTAL", "QTDE", "PRECOCUSTO", "CODCLIENTE", "NRODOCUMENTO"
            ],
            [ImportFileType.Ppp] =
            [
                "ANO", "MES", "VENDEDOR", "SEGMENTO", "QTDE_CLIENTES", "QTDE_ITENS_SEGMENTO", "GRUPOS_COLOCADOS"
            ],
            [ImportFileType.Goals] =
            [
                "VENDEDOR", "MES", "ANO", "TIPOMETA", "DESCRICAO", "META", "ALCANCADO"
            ],
            [ImportFileType.GoalValues] = []
        };

    public static ImportValidationResult Validate(ImportFileType fileType, string csv)
    {
        if (fileType == ImportFileType.GoalValues)
        {
            return ValidateGoalValues(csv);
        }

        var header = csv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var providedHeaders = header?
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal) ?? [];

        var errors = RequiredHeaders[fileType]
            .Where(requiredHeader => !providedHeaders.Contains(requiredHeader))
            .Select(requiredHeader => new ImportValidationError($"Required column is missing: {requiredHeader}"))
            .ToList();

        if (fileType == ImportFileType.Power && !providedHeaders.Contains("GRUPO") && !providedHeaders.Contains("REDE"))
        {
            errors.Add(new ImportValidationError("Required column is missing: GRUPO or REDE"));
        }

        return errors.Count == 0 ? ImportValidationResult.Valid() : ImportValidationResult.Rejected(errors);
    }

    private static ImportValidationResult ValidateGoalValues(string csv)
    {
        const string requiredHeader = "MARCA;FATURAMENTO;POSITIVACAO;TROCA;TROCA_PERCENTUAL";
        var hasBrandTableHeader = csv
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Any(line => string.Equals(line.Trim().TrimStart('\uFEFF'), requiredHeader, StringComparison.OrdinalIgnoreCase));

        return hasBrandTableHeader
            ? ImportValidationResult.Valid()
            : ImportValidationResult.Rejected([new ImportValidationError($"Required column is missing: {requiredHeader}")]);
    }
}
